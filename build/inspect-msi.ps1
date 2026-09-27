<#
.SYNOPSIS
  Reads the tables of a built MSI and prints the facts that matter, without installing it.

.DESCRIPTION
  Installing this package registers a service and rewrites per-interface DNS, which is R2 in
  docs/testing/dns-safety.md and belongs in a VM or Windows Sandbox. Everything an installer is
  supposed to do is checkable straight out of the Windows Installer tables instead, so the pipeline
  (and a reviewer) can confirm a build is what we claim before anyone runs it.

  Read-only: the database is opened in mode 0, so nothing is written even if the file is writable.

.EXAMPLE
  pwsh -NoProfile -File build/inspect-msi.ps1 -Path artifacts\SimpleDNSCryptPlus-x64-0.9.0-rc.1.msi
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Path,

  # Fail the build when a fact is wrong, instead of only printing it.
  [switch] $Strict
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$absolute = (Resolve-Path -LiteralPath $Path).Path
$installer = New-Object -ComObject WindowsInstaller.Installer
$invoke = [System.Reflection.BindingFlags]::InvokeMethod

# Mode 0 = read-only database open, so nothing is written even if the file is writable.
$database = $installer.GetType().InvokeMember('OpenDatabase', $invoke, $null, $installer, @($absolute, 0))

function Get-MsiColumn {
  <#
   Returns the selected column of every row, as plain strings. One column per query on purpose:
   Windows Installer's query language has no COUNT aggregate, so a row count is just a value count,
   and PowerShell unrolls a single-row multi-column result into a flat string list that would then
   be indexed as characters. StringData is reached through PowerShell's COM dispatch because
   InvokeMember cannot see it at all (DISP_E_MEMBERNOTFOUND - it is a parameterized property).
  #>
  param(
    [string] $Query,
    # MsiServiceControl exists only once something declares a ServiceControl, and the licence dialog
    # only once a UI is linked: for those, "absent" is a fact to report, not a broken audit.
    [switch] $AllowMissing
  )

  $view = $null
  try {
    $view = $database.GetType().InvokeMember('OpenView', $invoke, $null, $database, @($Query))
    # Execute and Close are void to a human but not to PowerShell: left unsuppressed they land in the
    # pipeline and the function's single return value becomes a 3-element array.
    $null = $view.Execute()
  } catch {
    if ($AllowMissing) { return @() }
    throw "query failed: $Query`n$($_.Exception.Message)"
  }
  if ($null -eq $view) {
    if ($AllowMissing) { return @() }
    throw "query rejected: $Query"
  }

  $values = New-Object System.Collections.Generic.List[string]
  while ($true) {
    $record = $view.Fetch()
    if ($null -eq $record) { break }
    $values.Add([string]$record.StringData(1))
  }
  $null = $view.Close()
  return $values.ToArray()
}

function Get-MsiProperty {
  param([string] $Name)
  # @() and a single [string] cast rather than $rows[0][0]: a one-row one-column result arrives
  # unrolled to a bare string, and indexing that again would return its first character.
  $sql = "SELECT Value FROM Property WHERE Property='$Name'"
  $rows = @(Get-MsiColumn $sql)
  if ($rows.Count -eq 0) { return $null }
  return [string]$rows[0]
}

# Every query selects exactly one column on purpose: a single-row result comes back unrolled into a
# flat string array, so a two-column query would silently turn into a list of characters when
# indexed. Multi-value facts are read as parallel queries and zipped below.
$fileRows      = @(Get-MsiColumn 'SELECT File FROM File')
$componentRows = @(Get-MsiColumn 'SELECT Component FROM Component')
$serviceName   = @(Get-MsiColumn 'SELECT Name FROM ServiceControl')
$serviceEvent  = @(Get-MsiColumn 'SELECT Event FROM ServiceControl')

$iconRows      = @(Get-MsiColumn 'SELECT Name FROM Icon')
$dialogRows    = @(Get-MsiColumn "SELECT Dialog_ FROM Control WHERE Dialog_='LicenseAgreementDlg'")
$mediaRows     = @(Get-MsiColumn 'SELECT Cabinet FROM Media')
$installDir    = @(Get-MsiColumn "SELECT DefaultDir FROM Directory WHERE Directory='INSTALLFOLDER'")
# The ServiceControl table, not MsiServiceControl: this MSI does not install the service (the app
# does), it only owns taking it down. Which combination of start/stop/remove/uninstall/reap the
# Event integer encodes is not documented in the MSI SDK the way the column names are, so it is
# printed as a raw value and only its presence is asserted - inventing a bit table here would make
# the audit lie about a good package.
$serviceRows = @()
for ($i = 0; $i -lt $serviceName.Count; $i++) {
  $event = if ([string]::IsNullOrWhiteSpace($serviceEvent[$i])) { '0' } else { $serviceEvent[$i] }
  $serviceRows += ,@($serviceName[$i], $event)
}

$info = [ordered]@{
  file                = $absolute
  sizeMB              = [math]::Round((Get-Item -LiteralPath $absolute).Length / 1MB, 1)
  productName         = Get-MsiProperty 'ProductName'
  productVersion      = Get-MsiProperty 'ProductVersion'
  manufacturer        = Get-MsiProperty 'Manufacturer'
  allUsers            = Get-MsiProperty 'ALLUSERS'
  upgradeCode         = Get-MsiProperty 'UpgradeCode'
  productCode         = Get-MsiProperty 'ProductCode'
  arpIcon             = Get-MsiProperty 'ARPPRODUCTICON'
  aboutUrl            = Get-MsiProperty 'ARPURLINFOABOUT'
  updateUrl           = Get-MsiProperty 'ARPURLUPDATEABOUT'
  files               = $fileRows.Count
  components          = $componentRows.Count
  serviceControlRows  = @($serviceRows | ForEach-Object { "$($_[0]) event=$($_[1])" })
  iconStreams         = $iconRows.Count
  licenseAgreementDlg = $dialogRows.Count
  # Cabinet '#' means the payload is embedded in the MSI itself rather than a sibling .cab.
  cabinet             = if (@($mediaRows).Count) { [string]@($mediaRows)[0] } else { '(no media row)' }
  targetDir           = if (@($installDir).Count) { [string]@($installDir)[0] } else { '(none)' }
}

$info.GetEnumerator() | ForEach-Object { "{0,-20} {1}" -f $_.Key, ($_.Value -join ', ') }

$problems = @()
if ($info.productName    -ne 'Simple DNSCrypt Plus') { $problems += "ProductName is '$($info.productName)'" }
if ($info.manufacturer   -ne 'Esperion')             { $problems += "Manufacturer is '$($info.manufacturer)'" }
if ($info.allUsers       -ne '1')                   { $problems += "ALLUSERS is '$($info.allUsers)', a per-machine install needs 1" }
if ($info.files          -lt 550)                   { $problems += "only $($info.files) files in the File table" }
if ($info.serviceControlRows.Count -ne 1)           { $problems += "expected 1 ServiceControl row, got $($info.serviceControlRows.Count)" }
if ($serviceName.Count -eq 1 -and $serviceName[0] -ne 'dnscrypt-proxy') {
  $problems += "ServiceControl targets '$($serviceName[0])', expected the real service name dnscrypt-proxy"
}
if ($info.licenseAgreementDlg -eq 0)                { $problems += "no LicenseAgreementDlg: the licence page is not in the UI" }
if ($info.iconStreams    -eq 0)                     { $problems += "no Icon stream: ARP would show a generic icon" }
if ($info.upgradeCode    -eq 'b561df39-7e27-44e4-978d-22df6eea11b4') { $problems += "instant.sc's UpgradeCode is in this package" }

if ($problems.Count) {
  Write-Host ""
  $problems | ForEach-Object { Write-Host "PROBLEM: $_" }
  if ($Strict) { throw "$($problems.Count) problem(s) in $absolute" }
} else {
  Write-Host "`ninspect-msi: all checks passed"
}

<#
.SYNOPSIS
  Builds one self-contained portable zip of Simple DNSCrypt Plus.

.DESCRIPTION
  Publishes the app for one architecture, packs it into the shipped zip layout, writes a
  SHA256SUMS entry and enforces an artifact size budget.

  Both CI and local verification call this, so what the pipeline does is runnable on a workstation.

  Note the zip is deliberately a folder layout, NOT PublishSingleFile: AppBootstrapper.cs:74 and
  Config/Global.cs:110 both derive every path from Assembly.GetExecutingAssembly().Location, which
  is an empty string under single-file publish. See docs/adr/0002-portable-only-no-msi.md.

.EXAMPLE
  pwsh -NoProfile -File build/build-portable.ps1 -Arch x64 -Version 1.0.0
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('x64', 'x86')]
  [string] $Arch,

  [string] $Version = '0.0.0-dev',

  [string] $OutDir,

  # Measured on this repository: x64 ~93 MB, x86 ~88 MB compressed. WPF cannot be trimmed, so this
  # is a regression tripwire rather than a shipping target - raise it deliberately, never to pass.
  [int] $SizeBudgetMB = 110
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'artifacts' }

$rid = "win-$Arch"
$publishDir = Join-Path $OutDir "portable-$Arch"
$zipName = "SimpleDNSCryptPlus-$Arch-$Version-portable.zip"
$zipPath = Join-Path $OutDir $zipName

# AssemblyVersion and FileVersion take only major[.minor[.build[.revision]]], so handing the whole
# tag to them breaks the compile with CS7034 the first time anyone tags a prerelease. Only the
# informational version may carry the suffix - and only the zip name and update manifest need it.
if ($Version -notmatch '^(\d+\.\d+\.\d+)') {
  throw "version '$Version' does not start with a numeric major.minor.patch, so no assembly version can be derived from it"
}
$numericVersion = $Matches[1]

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "publish $rid self-contained (single-file is deliberately not used)"
dotnet publish (Join-Path $repoRoot 'SimpleDnsCrypt') `
  -c Release -r $rid --self-contained true `
  -p:Version=$Version `
  -p:AssemblyVersion=$numericVersion `
  -p:FileVersion=$numericVersion `
  -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }

# Uninstall.exe is what restores each network interface to DHCP-supplied DNS. Upstream shipped it
# inside the MSI, and instant.sc's WiX project harvested it from a publish folder; with no MSI, the
# portable zip is the only place it can live, and without it a user has no way to undo a bad DNS
# setting except by hand. See docs/adr/0002-portable-only-no-msi.md.
Write-Host "publish Uninstall helper for $rid"
dotnet publish (Join-Path $repoRoot 'Uninstall') `
  -c Release -r $rid --self-contained true `
  -p:Version=$Version `
  -p:AssemblyVersion=$numericVersion `
  -p:FileVersion=$numericVersion `
  -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish of the Uninstall helper failed for $rid" }

foreach ($required in 'SimpleDnsCryptPlus.exe', 'Uninstall.exe') {
  if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required))) {
    throw "portable layout is missing $required"
  }
}

# Fail loudly if the digest-pinned proxy binary did not make it into the layout.
foreach ($proxyExe in 'dnscrypt-proxy64.exe', 'dnscrypt-proxy86.exe') {
  $p = Join-Path $publishDir "dnscrypt-proxy\$proxyExe"
  if (-not (Test-Path -LiteralPath $p)) {
    throw "publish output is missing dnscrypt-proxy\$proxyExe - run build/fetch-proxy.ps1 first"
  }
}

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Write-Host "packing $zipName"
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ("  {0} = {1} MB (budget {2} MB)" -f $zipName, $sizeMB, $SizeBudgetMB)
if ($sizeMB -gt $SizeBudgetMB) {
  throw "artifact $zipName is $sizeMB MB, over the $SizeBudgetMB MB budget. Investigate before raising the budget."
}

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()

# One line per artifact, replacing any previous entry for the same zip so re-running this script
# is idempotent. The file is per-architecture on purpose: CI builds each arch in its own job, and
# two jobs both writing SHA256SUMS.txt would collide when their artifacts are merged. The release
# job concatenates the per-arch files into the single published SHA256SUMS.txt.
$sumsPath = Join-Path $OutDir "SHA256SUMS-$Arch.txt"
$existing = @()
if (Test-Path -LiteralPath $sumsPath) {
  $existing = @(Get-Content -LiteralPath $sumsPath |
    Where-Object { $_.Trim() -and (($_ -split '\s+')[-1]) -ne $zipName })
}
$lines = @($existing + "$hash  $zipName")
# WriteAllText defaults to UTF-8 without BOM; Set-Content -Encoding utf8 emits a BOM on Windows
# PowerShell, which breaks `sha256sum -c`.
[System.IO.File]::WriteAllText($sumsPath, (($lines -join "`n") + "`n"))

Write-Host "  sha256 = $hash"
Write-Host "built: $zipPath"

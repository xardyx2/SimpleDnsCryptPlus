<#
.SYNOPSIS
  Builds one per-machine MSI of Simple DNSCrypt Plus from an existing portable publish layout.

.DESCRIPTION
  Takes the folder build/build-portable.ps1 already published and packages it as a Windows
  Installer package: heat harvests the payload into a component group, candle compiles it with
  Product.wxs, light links the MSI. Nothing here recompiles the app - the MSI and the zip are built
  from the same publish output, so an installed copy and an unzipped copy cannot diverge.

  Uninstall.exe is deliberately excluded from the harvest and installed as its own component with a
  fixed GUID, because Product.wxs references it by file id to run it on uninstall. A file in two
  components is a hard linker error, which is the safety net if that exclusion ever breaks.

  The toolset is the digest-pinned copy from tools/wix.lock.json, unpacked by build/fetch-wix.ps1.

.EXAMPLE
  pwsh -NoProfile -File build/build-msi.ps1 -Arch x64 -Version 0.9.0
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('x64', 'x86')]
  [string] $Arch,

  [string] $Version = '0.0.0-dev',

  # Where build-portable.ps1 left its publish folder.
  [string] $PublishDir,

  [string] $OutDir,

  # Regression tripwire, same idea as build-portable.ps1: the MSI carries the same self-contained
  # runtime as the zip, minus zip compression, so it is expected to be larger than the zip.
  [int] $SizeBudgetMB = 260
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256FileHex {
  param([string] $Path)
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
      (([System.BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '')).ToLowerInvariant()
    } finally { $stream.Dispose() }
  } finally { $sha.Dispose() }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'artifacts' }
if (-not $PublishDir) { $PublishDir = Join-Path $OutDir "portable-$Arch" }

# Windows Installer's ProductVersion is major.minor.build with no room for a prerelease label, so
# '0.9.0-rc.1' becomes '0.9.0'. Two candidates of the same core version therefore look identical to
# the upgrade logic, and installing the newer one over the older is a side-by-side refusal rather
# than an upgrade. Say so loudly instead of letting someone discover it on a test box.
$msiVersion = ($Version -replace '-.*$', '')
if ($msiVersion -ne $Version) {
  Write-Warning @"
$Version is a prerelease: the MSI ProductVersion will be $msiVersion, which Windows Installer cannot
tell apart from a $msiVersion candidate built earlier. Uninstall the older one first, or ship the
MSI only from stable tags.
"@
}

foreach ($needed in @('SimpleDnsCryptPlus.exe', 'Uninstall.exe')) {
  if (-not (Test-Path -LiteralPath (Join-Path $PublishDir $needed))) {
    throw "publish layout '$PublishDir' has no $needed - run build/build-portable.ps1 -Arch $Arch -Version $Version first"
  }
}

& (Join-Path $PSScriptRoot 'fetch-wix.ps1')
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'tools/wix.lock.json') -Raw | ConvertFrom-Json
$wix = Join-Path $repoRoot $lock.unpackedDir

$work = Join-Path $OutDir "msi-$Arch"
$stage = Join-Path $work 'payload'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Write-Host "staging payload for $Arch from $PublishDir"
# Get-ChildItem rather than Copy-Item -Path "$PublishDir\*": -LiteralPath does not expand a
# wildcard, and -Path would misread any filename containing [ ] or ?.
Get-ChildItem -LiteralPath $PublishDir -Force | Copy-Item -Destination $stage -Recurse -Force
# Installed by its own component in Product.wxs so the uninstall custom action has a stable file id.
Remove-Item -LiteralPath (Join-Path $stage 'Uninstall.exe') -Force

$exeCount = @(Get-ChildItem -LiteralPath $stage -Recurse -File).Count
if ($exeCount -lt 100) {
  throw "staged payload has only $exeCount files; a self-contained WPF publish is hundreds - the copy failed"
}
Write-Host "  $exeCount files staged"

$candle = Join-Path $wix 'candle.exe'
$light = Join-Path $wix 'light.exe'
$heat = Join-Path $wix 'heat.exe'
$uiExt = Join-Path $wix 'WixUIExtension.dll'
$wxs = Join-Path $repoRoot 'Installer/Product.wxs'
$license = Join-Path $repoRoot 'Installer/License.rtf'
$icon = Join-Path $repoRoot 'SimpleDnsCrypt/Images/simplednscrypt.ico'

foreach ($path in @($candle, $light, $heat, $uiExt, $wxs, $license, $icon)) {
  if (-not (Test-Path -LiteralPath $path)) { throw "missing required input: $path" }
}

# A directory Id of ProgramFiles64Folder is only correct for a 64-bit package; the x86 package must
# land in the 32-bit view so a WoW install behaves like every other x86 Windows Installer package.
$programFiles = if ($Arch -eq 'x64') { 'ProgramFiles64Folder' } else { 'ProgramFilesFolder' }
$defineVersion = "Version=$msiVersion"
$defineBasePath = "BasePath=$stage"
$uninstallExe = Join-Path $PublishDir 'Uninstall.exe'

$vars = @(
  "-d$defineVersion",
  "-dPlatform=$Arch",
  "-dProgramFilesFolder=$programFiles",
  "-d$defineBasePath",
  "-dUninstallExe=$uninstallExe",
  "-dLicenseRtf=$license",
  "-dIconPath=$icon"
)

Write-Host "harvesting payload (heat)"
$fragment = Join-Path $work 'payload.wxs'
# -gg stable component GUIDs derived from the file identity, so rebuilding does not churn them
# -srd keep the staged root out of the tree; files land under INSTALLFOLDER directly
# -cg  emit the ComponentGroup Product.wxs references; heat ignores an unrecognised switch silently,
#      so this is spelled exactly as the tool documents it rather than as a long name
# -var parameterize the source path, so the fragment carries no absolute build-machine path
& $heat dir $stage -gg -srd -scom -sreg -dr INSTALLFOLDER -cg SdcPlusPayload -var var.BasePath -out $fragment
if ($LASTEXITCODE -ne 0) { throw "heat failed with exit code $LASTEXITCODE" }

$groups = ([regex]::Matches((Get-Content -LiteralPath $fragment -Raw), '<ComponentGroup')).Count
if ($groups -ne 1) {
  throw "heat produced $groups ComponentGroup elements, expected 1 - Product.wxs would not resolve"
}

$objs = Join-Path $work 'obj'
New-Item -ItemType Directory -Force -Path $objs | Out-Null

Write-Host "compiling (candle)"
# No -out: candle writes one .wixobj per source into its current directory, and a single argument
# with the flag and path glued together ("-outD:\...") arrives at the native command with its leading
# dash eaten by the shell. Running candle from the obj directory is both simpler and that footgun's
# absence.
Push-Location -LiteralPath $objs
try {
  & $candle -nologo -arch $Arch $vars '-ext' $uiExt $wxs $fragment
  $candleExit = $LASTEXITCODE
} finally {
  Pop-Location
}
if ($candleExit -ne 0) { throw "candle failed with exit code $candleExit" }

$msiName = "SimpleDNSCryptPlus-$Arch-$Version.msi"
$msiPath = Join-Path $OutDir $msiName

Write-Host "linking (light) -> $msiName"
$wixobjs = @(Get-ChildItem -LiteralPath $objs -Filter *.wixobj | ForEach-Object { $_.FullName })
if ($wixobjs.Count -ne 2) {
  throw "expected 2 compiled wixobj files (product + payload), found $($wixobjs.Count): $($wixobjs -join ', ')"
}
# BasePath is a preprocessor variable in the harvested fragment, so it has to be defined again at
# link time - candle and light are separate processes and neither inherits the other's defines.
& $light -nologo "-dBasePath=$stage" '-out' $msiPath '-ext' $uiExt $wixobjs
if ($LASTEXITCODE -ne 0) { throw "light failed with exit code $LASTEXITCODE" }

$sizeMB = [math]::Round((Get-Item -LiteralPath $msiPath).Length / 1MB, 1)
Write-Host ("  {0} = {1} MB (budget {2} MB)" -f $msiName, $sizeMB, $SizeBudgetMB)
if ($sizeMB -gt $SizeBudgetMB) {
  throw "artifact $msiName is $sizeMB MB, over the $SizeBudgetMB MB budget. Investigate before raising the budget."
}

$hash = Get-Sha256FileHex -Path $msiPath

# Appended to the same per-arch sums file build-portable.ps1 owns, in its format: the release job
# concatenates these into the published SHA256SUMS.txt, and entries are matched by filename.
$sumsPath = Join-Path $OutDir "SHA256SUMS-$Arch.txt"
$existing = @()
if (Test-Path -LiteralPath $sumsPath) {
  $existing = @(Get-Content -LiteralPath $sumsPath |
    Where-Object { $_.Trim() -and (($_ -split '\s+')[-1]) -ne $msiName })
}
$lines = @($existing + "$hash  $msiName")
[System.IO.File]::WriteAllText($sumsPath, (($lines -join "`n") + "`n"))

Write-Host "  sha256 = $hash"
Write-Host "built: $msiPath"

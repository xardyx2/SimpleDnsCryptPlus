<#
.SYNOPSIS
  Unpacks the digest-pinned WiX Toolset used to build the MSI.

.DESCRIPTION
  Reads tools/wix.lock.json, downloads the pinned release asset, verifies its SHA-256 against the
  lock file, and unpacks it into .tools/wix314. Verification is fail-closed: a digest mismatch or a
  missing required tool aborts with a non-zero exit code, so a tampered archive can never reach a
  build. The unpacked dir records the digest it was verified against in .wix-verified; a later lock
  change makes that marker stale and forces a re-verify.

  Same shape and rationale as build/fetch-proxy.ps1: downloads, hashing and unzip all go straight to
  .NET types because this script may run under a Windows PowerShell host without module auto-loading.

.EXAMPLE
  pwsh -NoProfile -File build/fetch-wix.ps1
#>
[CmdletBinding()]
param(
  # Cache for the downloaded archive so repeated builds do not re-fetch.
  [string] $CacheDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256FileHex {
  param([string] $Path)
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
      (Convert-ByteArrayToHex -Bytes ($sha.ComputeHash($stream)))
    } finally { $stream.Dispose() }
  } finally { $sha.Dispose() }
}

function Convert-ByteArrayToHex {
  param([byte[]] $Bytes)
  ([System.BitConverter]::ToString($Bytes) -replace '-', '').ToLowerInvariant()
}

function Expand-ZipArchive {
  param([string] $Path, [string] $Destination)
  if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
  }
  [System.IO.Compression.ZipFile]::ExtractToDirectory($Path, $Destination)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$lockPath = Join-Path $repoRoot 'tools/wix.lock.json'

if (-not (Test-Path -LiteralPath $lockPath)) {
  throw "lock file not found: $lockPath"
}

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$toolDir = Join-Path $repoRoot $lock.unpackedDir
$marker = Join-Path $toolDir '.wix-verified'

if (-not $CacheDir) {
  $CacheDir = Join-Path ([System.IO.Path]::GetTempPath()) 'sdc-plus-wix-cache'
}
New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null

if ((Test-Path -LiteralPath $marker) -and ((Get-Content -LiteralPath $marker -Raw).Trim() -eq $lock.sha256)) {
  Write-Host "fetch-wix: $($lock.version) already unpacked and verified at $toolDir"
  exit 0
}

$archive = Join-Path $CacheDir $lock.asset
$url = "https://github.com/$($lock.repository)/releases/download/$($lock.version)/$($lock.asset)"

Write-Host "fetch-wix: $url"
if (Test-Path -LiteralPath $archive) {
  $cachedHash = Get-Sha256FileHex -Path $archive
  if ($cachedHash -ne $lock.sha256) {
    Write-Warning "  cached copy has hash $cachedHash, not the pinned $($lock.sha256); re-downloading"
    Remove-Item -LiteralPath $archive -Force
  }
}

if (-not (Test-Path -LiteralPath $archive)) {
  $client = New-Object System.Net.WebClient
  try { $client.DownloadFile($url, $archive) } finally { $client.Dispose() }
}

$actual = Get-Sha256FileHex -Path $archive
if ($actual -ne $lock.sha256) {
  Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
  throw @"

DIGEST MISMATCH for the WiX toolset - refusing to use this download.
  expected (pinned in $lockPath): $($lock.sha256)
  actual                         : $actual
If the project genuinely published a new release, update tools/wix.lock.json only after checking
the release page at $($lock.releaseUrl) - the toolset builds the signed package users install.
"@
}
Write-Host "  digest OK ($actual)"

if (Test-Path -LiteralPath $toolDir) { Remove-Item -LiteralPath $toolDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
Expand-ZipArchive -Path $archive -Destination $toolDir

foreach ($tool in $lock.requiredTools) {
  $path = Join-Path $toolDir $tool
  if (-not (Test-Path -LiteralPath $path)) {
    throw "$($lock.asset) did not contain $tool (looked in $path)"
  }
}

[System.IO.File]::WriteAllText($marker, $actual)
Write-Host "fetch-wix: ready at $toolDir"

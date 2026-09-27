<#
.SYNOPSIS
  Fetches the dnscrypt-proxy binaries that used to be committed to this repository.

.DESCRIPTION
  Reads tools/dnscrypt-proxy.lock.json, downloads each pinned release asset, verifies its SHA-256
  against the lock file, and copies the executable out to
  SimpleDnsCrypt/dnscrypt-proxy/dnscrypt-proxy{64,86}.exe.

  Verification is fail-closed: a digest mismatch, an unexpected member path, or a missing asset
  all abort with a non-zero exit code. Nothing is copied unless the archive hash matched, so a
  tampered or truncated download can never reach a build output.

  Set -Skip (or the SkipDnscryptProxyDownload MSBuild property / environment variable) for an
  offline or source-only build; the executables are then left as they are on disk.

.EXAMPLE
  pwsh -NoProfile -File build/fetch-proxy.ps1 -Arch x64
#>
[CmdletBinding()]
param(
  [ValidateSet('x64', 'x86', 'both')]
  [string] $Arch = 'both',

  # Cache for downloaded archives so repeated builds do not re-fetch.
  [string] $CacheDir,

  [switch] $Skip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Skip -or $env:SkipDnscryptProxyDownload -eq 'true') {
  Write-Host 'fetch-proxy: skipped (SkipDnscryptProxyDownload)'
  exit 0
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$lockPath = Join-Path $repoRoot 'tools/dnscrypt-proxy.lock.json'
$targetDir = Join-Path $repoRoot 'SimpleDnsCrypt/dnscrypt-proxy'

if (-not (Test-Path -LiteralPath $lockPath)) {
  throw "lock file not found: $lockPath"
}

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json

if (-not $CacheDir) {
  $CacheDir = Join-Path ([System.IO.Path]::GetTempPath()) 'sdc-plus-proxy-cache'
}
New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$wanted = if ($Arch -eq 'both') { @('x64', 'x86') } else { @($Arch) }

foreach ($name in $wanted) {
  $spec = $lock.artifacts.$name
  if (-not $spec) { throw "lock file has no entry for '$name'" }

  $url = "https://github.com/$($lock.repository)/releases/download/$($lock.version)/$($spec.asset)"
  $archive = Join-Path $CacheDir $spec.asset

  Write-Host "fetch-proxy [$name] $($spec.asset) from $url"
  if (Test-Path -LiteralPath $archive) {
    $cachedHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($cachedHash -ne $spec.sha256) {
      Write-Warning "  cached copy has hash $cachedHash, not the pinned $($spec.sha256); re-downloading"
      Remove-Item -LiteralPath $archive -Force
    }
  }

  if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
  }

  $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actual -ne $spec.sha256) {
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    throw @"

DIGEST MISMATCH for $name - refusing to use this download.
  expected (pinned in $($lockPath)): $($spec.sha256)
  actual                            : $actual
If upstream genuinely re-published this release, update the lock file only after
verifying the detached minisign signature - see docs/proxy-supply-chain.md.
"@
  }
  Write-Host "  digest OK ($actual)"

  $extractDir = Join-Path $CacheDir ("extract-" + $name)
  if (Test-Path -LiteralPath $extractDir) { Remove-Item -LiteralPath $extractDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
  Expand-Archive -LiteralPath $archive -DestinationPath $extractDir

  $member = Join-Path $extractDir $spec.memberPath
  if (-not (Test-Path -LiteralPath $member)) {
    throw "archive $($spec.asset) does not contain the expected member '$($spec.memberPath)'"
  }

  $dest = Join-Path $targetDir $spec.destinationName
  Copy-Item -LiteralPath $member -Destination $dest -Force
  $item = Get-Item -LiteralPath $dest
  Write-Host "  -> $dest ($('{0:N0}' -f $item.Length) bytes)"
}

Write-Host 'fetch-proxy: done'

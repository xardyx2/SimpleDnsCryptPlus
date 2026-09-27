<#
.SYNOPSIS
  Writes the update manifest that a release publishes as a download asset.

.DESCRIPTION
  One manifest per architecture (update-x64.json / update-x86.json). It is deliberately JSON and not
  YAML: the application already carries Newtonsoft.Json, and the old YAML manifest was the only thing
  keeping YamlDotNet in the product.

  The signature travels inline rather than as a second URL. A separate signature URL is a second
  object someone with release access could swap independently of the payload it is supposed to prove.

  Note what is NOT signed here: the manifest itself. The trust anchor is the minisign public key
  compiled into the application, applied to the downloaded zip, so an attacker who can edit the
  manifest can still not produce a zip that passes - they can only make the app report nothing
  useful.

.EXAMPLE
  pwsh -NoProfile -File build/make-update-manifest.ps1 -Arch x64 -Version 1.0.0
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('x64', 'x86')]
  [string] $Arch,

  [Parameter(Mandatory = $true)]
  [string] $Version,

  [string] $ArtifactDir = 'artifacts',

  [string] $Repository = 'xardyx2/SimpleDnsCryptPlus',

  # Defaults to <ArtifactDir>/update-<Arch>.json.
  [string] $OutFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$zipName = "SimpleDNSCryptPlus-$Arch-$Version-portable.zip"
$zipPath = Join-Path $ArtifactDir $zipName
$sigPath = "$zipPath.minisig"

if (-not (Test-Path -LiteralPath $zipPath)) {
  throw "no artifact at $zipPath - build it first (build/build-portable.ps1)"
}

if (-not (Test-Path -LiteralPath $sigPath)) {
  throw "no signature at $sigPath. A manifest without a signature would be believed by nothing, " +
        "so refusing to write one is the only honest answer."
}

# The name the zip is published under has to be the name the signature was made over, because a
# pre-hashed minisign signature records its file name and the app enforces that binding.
if ((Split-Path -Leaf $zipPath) -ne $zipName) { throw "unexpected artifact name: $zipPath" }

$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$armored = Get-Content -LiteralPath $sigPath -Raw

$manifest = [ordered]@{
  format           = 'zip'
  version          = $Version
  releaseDate      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
  downloadUri      = "https://github.com/$Repository/releases/download/v$Version/$zipName"
  sha256           = $sha256
  signatureArmored = $armored
}

foreach ($property in $manifest.Keys) {
  if ([string]::IsNullOrWhiteSpace([string]$manifest[$property])) {
    throw "manifest field '$property' is empty; the app rejects a manifest with a missing field"
  }
}

if (-not $OutFile) { $OutFile = Join-Path $ArtifactDir "update-$Arch.json" }
$OutFile = [System.IO.Path]::GetFullPath($OutFile)

# UTF-8 without a BOM: a leading BOM makes System.Text.Json and Newtonsoft both fail to parse, which
# would surface to a user as "no updates" rather than as a build error.
[System.IO.File]::WriteAllText($OutFile, ($manifest | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding $false))

Write-Host "wrote $OutFile"
Write-Host "  version   = $Version"
Write-Host "  sha256    = $sha256"
Write-Host "  download  = $($manifest.downloadUri)"

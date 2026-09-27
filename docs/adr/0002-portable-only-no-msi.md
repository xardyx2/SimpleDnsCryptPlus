# ADR 0002: Ship portable zips, defer MSI entirely

Date: 2026-09-27
Status: Accepted

## Context

Every published build of Simple DNSCrypt users know came as an `.msi`: upstream 0.7.1 shipped
x86 and x64 MSI packages, and instant.sc's 0.8.2 shipped one with 11k downloads. This fork needs to
distribute something, and the obvious move is to keep making an MSI.

The MSI is not actually available to a fork:

- **Upstream never committed the installer project.** `DNSCrypt/SimpleDnsCrypt` contains two
  projects (`SimpleDnsCrypt`, `Uninstall`) and no WiX source. The `.msi` files were built by
  infrastructure that is not in the repository at all.
- **instant.sc did commit one** — `Installer/Installer.wixproj` and `Installer/Product.wxs` — but it
  is legacy WiX v3 format (`ToolsVersion 4.0`, importing
  `$(MSBuildExtensionsPath32)\Microsoft\WiX\v3.x\Wix.targets`, erroring out unless WiX v3.11 build
  tools are installed machine-wide). WiX v3 `.wixproj` cannot be built by `dotnet build`, and WiX
  is now at v7 with a v4→v7 that is a rewrite rather than a version bump.
- **The workflow that supposedly built it has never executed.** `instantsc/SimpleDnsCrypt` reports
  `actions/runs.total_count = 0`. The committed `dotnet-desktop.yml` is an untested template whose
  `msbuild Installer\Installer.wixproj` step has no evidence of ever having worked on a GitHub
  runner.

The chosen distribution model is also portable: self-contained, unzipped anywhere, no installer
database, no service registration side effects at install time.

## Decision

Ship **portable zips only** for `v0.9.0`, one per architecture, each with a SHA-256 in
`SHA256SUMS.txt`. Keep `Installer/` in the tree as a reference, referenced by **no** workflow.

## Consequences

### What we take on by not having an MSI

- **No automatic service cleanup on uninstall.** `Product.wxs` currently does
  `<ServiceControl Id="DnscryptServiceStopRemove" Name="dnscrypt-proxy" Remove="uninstall"/>`. With
  zips, the in-app uninstall and the bundled `Uninstall.exe` are the only ways to remove the
  `dnscrypt-proxy` Windows service. `build/build-portable.ps1` must therefore keep shipping
  `Uninstall.exe`, and the CI gate must actually exercise it.
- **Coexistence hazard.** A user with an MSI-installed copy who unzips this build ends up with two
  managers of one `dnscrypt-proxy` service and one `dnscrypt-proxy.toml`. The README says to
  uninstall the old copy first. This is also why the single-instance mutex string stays
  `"SimpleDnsCrypt"` rather than being renamed with the product.
- **No upgrade code.** Windows Installer's `UpgradeCode` handled replacing an older install. A zip
  cannot; that is what the Stage 4 in-app update check is for.
- **Nothing appears in "Apps & features"**, so users looking there will not find it.

### Why not just migrate to WiX v7 now

Migrating a pipeline that has never run, to a toolset whose v4→v7 is a rewrite, while simultaneously
moving the app to .NET 10, would put two unproven systems in the same change. The zip path is fully
verifiable today: `build/build-portable.ps1` runs locally and produces byte-reproducible archives
whose digests can be checked independently.

### Conditions that should reopen this

- Users ask for an MSI or for "Apps & features" registration in volume.
- An enterprise deployment need appears where per-machine install with silent flags is required.
- The in-app updater proves unreliable enough that re-installing from a package becomes the
  supported recovery path.

If revived: port to WiX v5+ SDK-style (buildable with `dotnet wix`), and mint a **new**
`UpgradeCode` — never reuse instant.sc's `b561df39-7e27-44e4-978d-22df6eea11b4`, because an MSI
claiming that upgrade code would silently replace or be replaced by an install this project does not
control. Install directory should be `SimpleDNSCryptPlus`, distinct from the old `SimpleDNSCrypt`.

## Related constraint: no PublishSingleFile

Independent of packaging, the portable zip must stay a **folder layout**.
`AppBootstrapper.cs:74` and `Config/Global.cs:110` both compute paths from
`Assembly.GetExecutingAssembly().Location`, which is the empty string under single-file publish —
so `InstallPath`, `DnsCryptFolderPath`, `DnsCryptLogFilePath` and the `configVersion.txt` path would
all silently resolve against whatever directory launched the process. Recorded here so nobody
"optimises" the zip into one file later.

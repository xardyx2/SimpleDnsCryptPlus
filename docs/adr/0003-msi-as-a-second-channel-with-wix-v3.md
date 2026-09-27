# ADR 0003: Ship an MSI as a second channel, built with WiX v3.14

Date: 2026-09-28
Status: Accepted — amends `0002-portable-only-no-msi.md`

## Context

ADR 0002 deferred the MSI entirely and named the conditions that should reopen it: users asking in
volume, an enterprise deployment need, or the in-app updater proving unreliable. None of those has
happened — `v0.9.0-rc.1` had been downloaded once at the time of writing. The fork moved anyway
because the cost estimate in ADR 0002 turned out to be wrong in one direction and right in another:

- **Wrong about cost.** instant.sc's WiX source is not a dead end. WiX v3.14.1 (2024-03) still builds
  it, ships as a portable `wix314-binaries.zip` needing no machine-wide install, and is **preinstalled
  on the GitHub `windows-2022` and `windows-2025` runner images**. Actions is free for a public repo
  (`billable.WINDOWS.total_ms = 0` on this repository's own runs), so a per-push MSI build costs
  nothing but minutes.
- **Right that v4+ is not free.** WiX v7 is SDK-style and nicer to author, and `dotnet build` refuses
  it outright: `error WIX7015: You must accept the Open Source Maintenance Fee (OSMF) EULA`. The EULA
  text shipped in the package binds only users whose revenue-generating use sits at or above
  US$10,000 gross per year, and it explicitly permits self-compiling from source under the MS-RL
  licence. Accepting it on behalf of a company is a commercial decision, not something a build script
  should sign.

The thing an MSI buys that a zip structurally cannot: an entry in **Apps & features**, automatic
removal of the `dnscrypt-proxy` service on uninstall, and a silent per-machine install path
(`msiexec /i … /qn`) for managed fleets.

## Decision

Ship a **per-machine MSI per architecture alongside the portable zips**, from the same publish
layout, so the two artifacts cannot contain different code.

- Toolset pinned by SHA-256 in `tools/wix.lock.json` and unpacked fail-closed by
  `build/fetch-wix.ps1` — the same supply-chain shape already used for dnscrypt-proxy. CI uses the
  pinned copy rather than the runner's preinstalled one, so a workstation and a runner build with
  identical bits.
- `build/build-msi.ps1` drives `heat`/`candle`/`light` directly. `Installer/Installer.wixproj` is
  deleted: its `BeforeBuild` republished win-x64 only, into a second folder, and it cannot supply the
  preprocessor variables `Product.wxs` now takes from the build — an MSBuild project that nothing can
  build is the trap ADR 0002 was written to avoid.
- **New identity, everywhere.** `Simple DNSCrypt Plus` / `Esperion` / `Program Files\SimpleDNSCryptPlus`
  / HKCU anchor under `Software\Esperion`. instant.sc's `UpgradeCode`
  `b561df39-7e27-44e4-978d-22df6eea11b4` is **not** reused, and their
  `<MajorUpgrade AllowDowngrades="yes"/>` is dropped. `Tests/InstallerIdentityTests.cs` enforces both
  against the source, with a canary package carrying the bad values to prove the guard can fail.
- The **zip stays the update-channel payload.** `build/make-update-manifest.ps1` resolves
  `SimpleDNSCryptPlus-<arch>-<version>-portable.zip` and writes `format = 'zip'`; the updater has no
  code path for applying an MSI, so adding one to the manifest would offer users an update it cannot
  install. The MSI is a release asset, signed and checksummed like everything else.
- The MSI gets the same treatment as the zips in CI: built and table-audited on every push
  (`build/inspect-msi.ps1 -Strict`), minisign-signed at release, and verified against
  `tools/keys/update.pub` by `verify-release.yml` after publication.

## Consequences

### What this costs

- **Two managers of one service.** A machine with both an MSI install and an unzipped copy has two
  folders writing one `dnscrypt-proxy.toml`. The README already says to uninstall a predecessor
  first; now it also says pick one channel.
- **Windows Installer cannot express a prerelease.** `ProductVersion` is `major.minor.build`, so
  `0.9.0-rc.1` installs as `0.9.0`. Two candidates of the same core version are indistinguishable to
  the upgrade logic, and `build-msi.ps1` warns rather than inventing an encoding. Real releases
  (`0.9.0`, `1.0.0`) are unaffected.
- **An EOL-class toolset.** v3.14.1 gets no feature work; the mitigation is that it is build-time
  only, digest-pinned, and produces a standard MSI that Windows Installer itself interprets.
- **Still no Authenticode.** An unsigned per-machine MSI triggers a louder UAC/SmartScreen prompt than
  an unsigned zip. That is the same trade-off already documented in `docs/AV-FALSE-POSITIVES.md`.
- **Roughly 175 MB more per release**, as GitHub release assets, which do not touch the Actions
  artifact allowance.

### What is still not proven

No human has installed or uninstalled this MSI. Reading its tables (identity, `ALLUSERS=1`, one
`ServiceControl` row for `dnscrypt-proxy`, licence dialog, embedded cabinet, 560 files) is a check on
the package, not on behaviour: the uninstall path runs a deferred `Uninstall.exe` that calls
`netsh … delete dns` on every interface, which is R2 in `docs/testing/dns-safety.md` and belongs in
Windows Sandbox or a checkpointed VM.

## Conditions that would revisit this

- WiX v3.14 stops working against a required Windows Installer feature → decide between paying the
  OSMF, self-compiling v7 from source, or Inno Setup (which asks a commercial licence above ~US$5,000
  revenue and emits `.exe`, not `.msi`).
- The in-app updater gains the ability to apply an MSI → the zip-only rule above can be relaxed.
- Anyone reports the two channels conflicting on a real machine → prefer retiring one over documenting
  the conflict harder.

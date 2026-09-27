# Changelog

All notable changes to **Simple DNSCrypt Plus** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions `0.1` through `0.7.1` belong to upstream Simple DNSCrypt by Christian Hermann, and
`0.7.2.3` through `0.8.2` to instant.sc's fork. They are listed below for continuity only — see
their respective repositories for authoritative histories.

## [Unreleased]

### Fixed
- The README still described the pre-candidate state of the fork: its dnscrypt-proxy badge read
  2.1.5 while `tools/dnscrypt-proxy.lock.json` pins 2.1.18, its release badge resolved to
  "no releases or repo not found" because the only published release is a prerelease, and the
  comparison table listed `.NET 10` and a running CI pipeline as work still to come. All three
  have shipped.
- The published verification command no longer fails on copy-paste. `update.pub` is not inside the
  portable zip — `build/build-portable.ps1` archives only the publish folder — so the snippet now
  fetches the key from the release tag first, and the archive names carry the real `-rc.1` suffix.
  `releases/latest` is left empty on purpose until a stable release exists, so the download links
  now point at the release list.
- `Tests/ReleaseDocConsistencyTests.cs` pins the README's proxy badge to the lock file, so the
  version claim cannot go stale in silence again.

## [0.9.0-rc.1] - 2026-09-27

The first published artifact of this fork, and deliberately a release candidate rather than `0.9.0`:
no person has driven the tray menu or the three drag-drop surfaces yet, the memory-bound fix has not
been soaked, and the settings GUI covers only part of what dnscrypt-proxy 2.1.18 accepts. What the
candidate contains:

### Added
- Fork of `DNSCrypt/SimpleDnsCrypt` merged with all 58 commits of `instantsc/SimpleDnsCrypt`, so
  neither upstream's resolver-list migration nor instantsc's modernization is lost.
- `global.json` pinning the .NET 10 SDK so local and CI builds agree.
- `NOTICE.md`, `SECURITY.md`, and this file.
- A README that states plainly what this fork is and what it cannot do.

### Changed
- Product identity is now **Simple DNSCrypt Plus**: `Global.ApplicationName`, assembly title and
  product, `AssemblyName` → `SimpleDnsCryptPlus` (so the executable is
  `SimpleDnsCryptPlus.exe`), manifest assembly identity, and copyright attribution extended to
  include both predecessors by name.
- Version baseline set to `0.9.0`. It has to sort above the `0.7.1` and `0.8.2` builds still in
  circulation so no one is offered a downgrade, and that is the whole claim the number makes: `.NET 10`,
  a new update channel and a real CI pipeline are all in place, but the tray and drag-drop surfaces
  have never been driven by a human, the memory-bound fix has not been soaked, and the GUI still
  writes only part of what dnscrypt-proxy can be told. `1.0.0` is reserved for when those close.

### Removed
- Upstream's `README.md` badges pointing at a personal AppVeyor project, portable-download links
  pointing at a personal Azure Blob storage account, and a PayPal donation address belonging to
  the original author.
- `.github/FUNDING.yml`, which routed to the original author's Open Collective.
- `SimpleDnsCrypt/DNSx64/` and `SimpleDnsCrypt/DNSx86/` — superseded by instantsc's relocated
  `SimpleDnsCrypt/dnscrypt-proxy/dnscrypt-proxy{64,86}.exe`.

### Intentionally **not** renamed
The Windows service name `dnscrypt-proxy`, the single-instance mutex string `"SimpleDnsCrypt"`,
and every configuration/rule/log filename (`dnscrypt-proxy.toml`, `query.log`, `blocked.log`,
`configVersion.txt`, …) keep their original names on purpose. Renaming any of them would orphan
existing user configuration or let two managers of the same service run at once. C# namespaces and
project folder names likewise stay put.

### Changed
- Target framework moved to `net10.0-windows10.0.19041` across all four projects. The platform
  version is load-bearing: plain `net10.0-windows` makes NuGet silently fall back to
  ReactiveUI.WPF 19.5.1's .NET Framework 4.8 assets.
- Dependency updates: MahApps.Metro 2.4.10→2.4.11, ReactiveProperty 9.3.4→9.9.0,
  NLog 5.2.5→6.2.1, Hardcodet.NotifyIcon.Wpf 1.1.0→2.0.1, gong-wpf-dragdrop 3.2.1→4.0.0,
  Caliburn.Micro 4.0.212→5.0.258, and `minisign-net` 1.0.0 added for the update channel.
  `ReactiveUI.WPF` deliberately stays at 19.5.1 — see `docs/adr/0001-stay-on-reactiveui-19.md`.
- The `dnscrypt-proxy` executables are no longer committed. They are downloaded by
  `build/fetch-proxy.ps1` and verified against SHA-256 digests pinned in
  `tools/dnscrypt-proxy.lock.json`, failing closed on mismatch. Bundled version goes 2.1.5 → 2.1.18.
- `build/build-portable.ps1` assembles the shipped zip and enforces an artifact size budget
  (measured: x64 93.3 MB, x86 88.1 MB).

### Fixed
- Query, domain-block and address-block log views grew without bound for the life of the process,
  behind [instantsc/SimpleDnsCrypt#19](https://github.com/instantsc/SimpleDnsCrypt/issues/19)
  ("4gb ram usage!"). Now capped at 1000 entries each, enforced at the type level so reverting it is
  a compile error.
- The log tail readers stalled permanently after dnscrypt-proxy rotated a log file —
  [DNSCrypt/SimpleDnsCrypt#287](https://github.com/DNSCrypt/SimpleDnsCrypt/issues/287), "Query log is
  broken until Simple DNSCrypt is restarted". The seek landed past EOF and the stale offset was
  re-recorded every 500 ms forever.
- The configuration migration overwrote a user's custom resolver `sources` list unconditionally;
  it now only replaces the untouched v2 default pair. This also removes a latent
  `IndexOutOfRangeException` on a single-URL source list.
- Tagalog was an unusable translation: `Translation.tgl.resx` produced a satellite that could never
  be found, because `CultureInfo("tgl").Name` normalises to `tl`. Renamed to
  `Translation.tl.resx` and now offered in the language dropdown.

### Added
- GitHub Actions `ci.yml` and `release.yml`. The test step asserts a non-zero test count, so a
  silently-skipped test project cannot produce a green build.
- `Uninstall.exe` ships in the portable zip. It was previously harvested by the MSI, and it is what
  restores each interface to DHCP-supplied DNS.
- Tests: 5 → 60. New guards cover Caliburn view resolution, translation coverage per culture, and
  consistency between `<AssemblyName>` and the 13 WPFLocalizeExtension references that depend on it.
- `docs/adr/0001-stay-on-reactiveui-19.md`, `docs/adr/0002-portable-only-no-msi.md`,
  `docs/proxy-supply-chain.md`.
- An in-app update channel authenticated by **this project's own** minisign key
  (`tools/keys/update.pub`, compiled in as `UpdateChannel.TrustedPublicKey`). Release zips carry a
  detached `.minisig`, and `update-x64.json` / `update-x86.json` published as release assets name the
  artifact, its SHA-256 and its signature. `tools/minisign-tool` generates and applies signatures with
  no `minisign` install needed, and CI checks its output against the reference CLI — including a
  deliberately tampered copy that the CLI must reject.
- `ApplicationUpdater` on the app side: an opt-out check at startup offers a newer release, which is
  downloaded into a sibling `_update\<version>\` folder and unpacked only after both the published
  hash and the signature check out. It never replaces the running executable.
- `settings_check_for_updates` and the updater strings. These live only in the neutral
  `Translation.resx`, and a test asserts every offered language still resolves them, which is what
  proves the invariant fallback a frozen translation set depends on.

### Removed
- `YamlDotNet` and the dead `UriYamlTypeConverter`. The update manifest was their only user, and the
  manifest is JSON now, so the dependency and its About-screen entry left with it.

### Added
- `docs/testing/dns-safety.md` (the R0/R1/R2 protocol for testing software that rewrites your DNS),
  `docs/AV-FALSE-POSITIVES.md`, `translations/README.md` and `docs/backlog-triage.md`.
- A translation key-drift guard: the 34 language files still carry an identical set of 191 keys, and a
  test fails if one diverges. `SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS=1` lifts the adapter blacklist so a
  VM guest lists interfaces at all, which is what makes the DNS-writing paths testable.

### Still open before `1.0.0`
- **The proxy's configuration surface is only partly ours.** `example-dnscrypt-proxy.toml` from 2.1.18
  documents settings we never model, and the notable omissions are the ones that define the 2.1 line:
  `pqdnscrypt` (post-quantum), `odoh_servers`, `enable_hot_reload`, `bootstrap_resolvers`, plus whole
  sections with no representation at all — `[schedules]`, `[monitoring_ui]`, `[ip_encryption]`,
  `[local_doh]`, `[captive_portals]` — and the `allowed_*` / `blocked_ips` half of the blacklist.
  Because `DnscryptProxyConfigurationManager` deserialises to a typed object and writes that object
  back, a key set by hand is deleted on the next save, comments included. Either the round-trip has to
  preserve what it does not understand, or the keys above have to become settings; until one of those
  is true, "bundles dnscrypt-proxy 2.1.18" must not be read as "exposes dnscrypt-proxy 2.1.18".
- A person has to look at the tray icon menu and the three drag-drop surfaces. Both libraries took a
  major bump, the app runs elevated, and UIPI stops a non-elevated automation from driving it.
- Announcing the fork where the users are (`DNSCrypt/SimpleDnsCrypt#554`/`#581`,
  `instantsc/SimpleDnsCrypt#27`). Drafted, deliberately not sent.
- The update channel's dry run against a real published release, which needs the first tag.

## [0.8.2] - 2023-11-17 — instant.sc
- Updated to .NET 8. Updated dependencies and dnscrypt-proxy to 2.1.5. Markup fixes and cleaner
  IP address display.

## [0.7.1] - 2020-04-11 — Christian Hermann
- Last release of the original project. dnscrypt-proxy 2.0.42, fallback-resolver dialog, tray
  mode, window-size memory.

[Unreleased]: https://github.com/xardyx2/SimpleDnsCryptPlus/compare/v0.9.0-rc.1...HEAD
[0.9.0-rc.1]: https://github.com/xardyx2/SimpleDnsCryptPlus/releases/tag/v0.9.0-rc.1
[0.8.2]: https://github.com/instantsc/SimpleDnsCrypt/releases/tag/0.8.2
[0.7.1]: https://github.com/DNSCrypt/SimpleDnsCrypt/releases/tag/0.7.1

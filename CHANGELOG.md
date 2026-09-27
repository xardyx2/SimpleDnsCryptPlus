# Changelog

All notable changes to **Simple DNSCrypt Plus** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions `0.1` through `0.7.1` belong to upstream Simple DNSCrypt by Christian Hermann, and
`0.7.2.3` through `0.8.2` to instant.sc's fork. They are listed below for continuity only — see
their respective repositories for authoritative histories.

## [Unreleased]

Work in progress on `master`. Nothing here is released yet.

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
- Version baseline set to `1.0.0`, deliberately greater than the `0.7.1` and `0.8.2` builds
  already in circulation.

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

### Planned before `1.0.0`
- Target framework `net10.0-windows` across all four projects.
- Dependency majors (ReactiveUI, gong-wpf-dragdrop, Hardcodet.NotifyIcon.Wpf, NLog, YamlDotNet).
- Bound the unbounded log collections behind upstream issue #19 (4 GB working set) and #287
  (query log dead until restart).
- Narrow `PatchHelper`'s config migration so it stops overwriting a user's custom `sources` list.
- GitHub Actions CI producing signed-by-minisign portable zips for x64 and x86, with the
  `dnscrypt-proxy` binaries fetched at build time against pinned SHA-256 digests.
- An in-app update channel authenticated by this project's own minisign key.

## [0.8.2] - 2023-11-17 — instant.sc
- Updated to .NET 8. Updated dependencies and dnscrypt-proxy to 2.1.5. Markup fixes and cleaner
  IP address display.

## [0.7.1] - 2020-04-11 — Christian Hermann
- Last release of the original project. dnscrypt-proxy 2.0.42, fallback-resolver dialog, tray
  mode, window-size memory.

[Unreleased]: https://github.com/esperion-agency/SimpleDnsCryptPlus
[0.8.2]: https://github.com/instantsc/SimpleDnsCrypt/releases/tag/0.8.2
[0.7.1]: https://github.com/DNSCrypt/SimpleDnsCrypt/releases/tag/0.7.1

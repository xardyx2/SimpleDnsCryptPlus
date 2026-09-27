[![license](https://img.shields.io/github/license/xardyx2/SimpleDnsCryptPlus.svg?style=flat-square)](LICENSE.md)
[![CI](https://github.com/xardyx2/SimpleDnsCryptPlus/actions/workflows/ci.yml/badge.svg)](https://github.com/xardyx2/SimpleDnsCryptPlus/actions/workflows/ci.yml)
[![release](https://img.shields.io/github/v/release/xardyx2/SimpleDnsCryptPlus.svg?style=flat-square&include_prereleases)](https://github.com/xardyx2/SimpleDnsCryptPlus/releases)
[![dnscrypt-proxy](https://img.shields.io/badge/dnscrypt--proxy-2.1.18-orange.svg?style=flat-square)](https://github.com/DNSCrypt/dnscrypt-proxy)

# Simple DNSCrypt Plus

![Simple DNSCrypt Logo](img/logo_with_text.png)

A simple management tool to configure [dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy) on Windows.

> **This is an independent, unofficial fork.** It is not affiliated with, endorsed by, or
> maintained by [Christian Hermann (bitbeans)](https://github.com/bitbeans),
> [instant.sc](https://github.com/instantsc), or the
> [DNSCrypt organization](https://github.com/DNSCrypt). The upstream
> [SimpleDnsCrypt](https://github.com/DNSCrypt/SimpleDnsCrypt) has not shipped a release since
> 0.7.1 (2020-04-11); this fork exists because that project is unmaintained and its users still
> need current binaries. Nothing here should be read as an official edition or successor.

## Why this fork

| | [upstream](https://github.com/DNSCrypt/SimpleDnsCrypt) | [instantsc](https://github.com/instantsc/SimpleDnsCrypt) | this fork (now) |
|---|---|---|---|
| Last release | 0.7.1 — Apr 2020 | 0.8.2 — Nov 2023 | `0.9.0-rc.1` — Sep 2026, prerelease; no stable yet |
| Target framework | .NET Framework 4.8 | .NET 8 | .NET 10 (`net10.0-windows10.0.19041`) |
| CI actually running | none in repo (AppVeyor lived on the author's personal account) | workflow committed, **never executed once** | GitHub Actions — `ci.yml` on every push, `release.yml` gated on a tag |
| Bundled dnscrypt-proxy | 2.1.15 | 2.1.5 | **2.1.18**, fetched at build time with pinned SHA-256 |
| Installer | MSI, source **not in repo** | MSI via WiX v3, unsigned | portable zip + minisign, plus a per-machine MSI (unsigned) |

Both predecessors did real work and are credited in [NOTICE.md](NOTICE.md) and
[CHANGELOG.md](CHANGELOG.md). This fork merges upstream's `master` together with all 58 commits
from `instantsc`, so it loses neither the WiX installer source and `SimpleDnsCrypt.Utils`
reimplementation nor upstream's resolver-list migration.

## Honest limitations — read before you rely on this

- **Artifacts are unsigned.** There is no Authenticode certificate. SmartScreen will warn, and
  some antivirus products report false positives for this class of app — it writes per-interface
  DNS settings under `HKLM` and ships a Go binary. See [docs/AV-FALSE-POSITIVES.md](docs/AV-FALSE-POSITIVES.md).
  Verifying the published SHA-256 and minisign signature is the intended check instead.
- **Existing installations cannot be upgraded remotely, ever.** Upstream 0.7.x polls
  `raw.githubusercontent.com/bitbeans/.../update.yml` and validates against *Christian Hermann's*
  minisign public key, which this fork does not and never will hold. 0.8.x builds have no
  updater at all. If you have an old copy, uninstall it and install a zip from here.
- **The GUI writes a subset of what dnscrypt-proxy 2.1.18 can do.** `dnscrypt-proxy.toml` is
  round-tripped through a typed model, so keys this app does not expose — `pqdnscrypt`,
  `odoh_servers`, `enable_hot_reload`, `bootstrap_resolvers`, `[schedules]`, `[monitoring_ui]`,
  `[ip_encryption]`, `[local_doh]`, `[captive_portals]`, and the `allowed_*` / `blocked_ips` side of
  the blacklist — cannot be set from here, and if you add them by hand they are erased the next time
  the app saves, comments included. This behaviour is inherited from 0.7.x/0.8.2, not a regression
  introduced here; closing it is a stated gate before `1.0.0`.
- **Settings do not migrate across the rename.** In .NET, user settings storage is tied to the
  binary identity. Because the executable is now `SimpleDnsCryptPlus.exe`, window position and
  similar preferences start fresh. Your `dnscrypt-proxy.toml` and rule files are unaffected —
  they live next to the executable and keep their original names.
- **Translations are frozen.** The old POEditor project is owned by upstream's author.
  `Resources/Translation.*.resx` in this repository is now the source of truth, and new strings
  ship English-only. See [translations/README.md](translations/README.md).
- **Two install channels, one Windows service.** Releases ship a portable zip *and* a per-machine
  MSI, built from the same publish layout. The zip stays the only thing the in-app updater will
  download, because the updater has no way to apply an MSI; the MSI exists for an Apps & features
  entry, automatic service removal on uninstall, and `msiexec /qn` fleet installs. Pick one — a
  portable copy and an MSI copy of this app manage the *same* `dnscrypt-proxy` service and the same
  `dnscrypt-proxy.toml`, and will conflict. Windows Installer cannot represent a prerelease suffix in
  `ProductVersion`, so candidate MSIs of the same core version do not upgrade each other. The MSI is
  built with WiX v3.14 because v4+ binary releases require accepting a revenue-based maintenance-fee
  EULA; see [docs/adr/0003-msi-as-a-second-channel-with-wix-v3.md](docs/adr/0003-msi-as-a-second-channel-with-wix-v3.md).
- **No human has installed the MSI yet.** Its tables are audited on every build
  (`build/inspect-msi.ps1`, identity, `ALLUSERS`, the service-cleanup row, the licence page), but the
  install, upgrade and uninstall paths have not been walked in a VM. Until they have, the zip is the
  recommended channel.
- **This app requires administrator rights** and will show a UAC prompt on every launch.

## Download

Portable, self-contained (no .NET installation needed):

- `SimpleDNSCryptPlus-x64-<version>-portable.zip`
- `SimpleDNSCryptPlus-x86-<version>-portable.zip`

Per-machine installer, registered in Apps & features and removable with `msiexec /x`:

- `SimpleDNSCryptPlus-x64-<version>.msi`
- `SimpleDNSCryptPlus-x86-<version>.msi`

`<version>` carries the prerelease suffix while the project is pre-`1.0.0`; the current build is
`SimpleDNSCryptPlus-x64-0.9.0-rc.1-portable.zip`.

Grab them from [Releases](https://github.com/xardyx2/SimpleDnsCryptPlus/releases). Note that
`/releases/latest` is empty until a stable release exists, and that is deliberate: a candidate must
never reach an installed copy as an automatic update offer.

Each release carries `SHA256SUMS.txt` and one `.minisig` per archive or installer. The public key is
**not** inside the download — it is published in this repository, so fetch it from the tag you
downloaded from, then verify. This needs the [minisign](https://github.com/jedisct1/minisign) CLI:

```powershell
$tag = 'v0.9.0-rc.1'
Invoke-WebRequest `
  "https://raw.githubusercontent.com/xardyx2/SimpleDnsCryptPlus/refs/tags/$tag/tools/keys/update.pub" `
  -OutFile update.pub

$payload = 'SimpleDNSCryptPlus-x64-0.9.0-rc.1-portable.zip'   # or the .msi of the same arch
Get-FileHash ".\$payload" -Algorithm SHA256
minisign -Vm ".\$payload" -x ".\$payload.minisig" -p .\update.pub
```

`Get-FileHash` prints uppercase; the release's `SHA256SUMS.txt` is lowercase. The two are equal
once you ignore case, and `minisign` must print `Good Signature` against the key above.

Unzip anywhere you like, run `SimpleDnsCryptPlus.exe` as administrator. To remove the service,
run `Uninstall.exe` from the same folder (or uninstall the service from inside the app).

## Building from source

Requires the .NET 10 SDK (pinned by `global.json`) on Windows:

```powershell
dotnet restore SimpleDnsCrypt.sln
dotnet build   SimpleDnsCrypt.sln -c Release
dotnet test    Tests/Tests.csproj -c Release
```

The `dnscrypt-proxy` binaries are **not** committed. A release build downloads them from
upstream and fails closed unless the digests match `tools/dnscrypt-proxy.lock.json`. For offline
or source-only builds, set `-p:SkipDnscryptProxyDownload=true`.

Before testing changes that touch live DNS, read [docs/testing/dns-safety.md](docs/testing/dns-safety.md). The app
installs a Windows service and rewrites per-interface DNS; testing it naively can leave your own
machine unable to resolve any name — which also blocks you from looking up the fix.

## Authors

* **Christian Hermann** — [bitbeans](https://github.com/bitbeans) — original author, 0.1–0.7.1 (2015–2020)
* **instant.sc** — [instantsc](https://github.com/instantsc) — .NET 8 migration, 0.7.2.3–0.8.2 (2021–2023)
* **Ardy S.** — [xardyx2](https://github.com/xardyx2) — this fork (2026–)

See also [Contributors.md](Contributors.md), including the translators whose work is carried
forward here.

## License

MIT — see [LICENSE.md](LICENSE.md) and [NOTICE.md](NOTICE.md).

## Thanks

* Frank Denis ([jedisct1](https://github.com/jedisct1)) for dnscrypt-proxy itself.
* Both previous maintainers, for building and then keeping alive the tool this fork continues.

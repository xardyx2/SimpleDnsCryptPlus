[![license](https://img.shields.io/github/license/xardyx2/SimpleDnsCryptPlus.svg?style=flat-square)](LICENSE.md)
[![release](https://img.shields.io/github/v/release/xardyx2/SimpleDnsCryptPlus.svg?style=flat-square)](https://github.com/xardyx2/SimpleDnsCryptPlus/releases/latest)
[![dnscrypt-proxy](https://img.shields.io/badge/dnscrypt--proxy-2.1.5-orange.svg?style=flat-square)](https://github.com/DNSCrypt/dnscrypt-proxy)

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

| | [upstream](https://github.com/DNSCrypt/SimpleDnsCrypt) | [instantsc](https://github.com/instantsc/SimpleDnsCrypt) | this fork (today → target) |
|---|---|---|---|
| Last release | 0.7.1 — Apr 2020 | 0.8.2 — Nov 2023 | none yet → `1.0.0` |
| Target framework | .NET Framework 4.8 | .NET 8 | .NET 8 → **.NET 10** |
| CI actually running | none in repo (AppVeyor lived on the author's personal account) | workflow committed, **never executed once** | none yet → GitHub Actions, release-gating |
| Bundled dnscrypt-proxy | 2.1.15 | 2.1.5 | 2.1.5 → **2.1.18**, fetched at build time with pinned SHA-256 |
| Installer | MSI, source **not in repo** | MSI via WiX v3, unsigned | portable zip + minisign (MSI deferred) |

Both predecessors did real work and are credited in [NOTICE.md](NOTICE.md) and
[CHANGELOG.md](CHANGELOG.md). This fork merges upstream's `master` together with all 58 commits
from `instantsc`, so it loses neither the WiX installer source and `SimpleDnsCrypt.Utils`
reimplementation nor upstream's resolver-list migration.

## Honest limitations — read before you rely on this

- **Artifacts are unsigned.** There is no Authenticode certificate. SmartScreen will warn, and
  some antivirus products report false positives for this class of app — it writes per-interface
  DNS settings under `HKLM` and ships a Go binary. See [docs/AV-FALSE-POSITIVES.md](docs).
  Verifying the published SHA-256 and minisign signature is the intended check instead.
- **Existing installations cannot be upgraded remotely, ever.** Upstream 0.7.x polls
  `raw.githubusercontent.com/bitbeans/.../update.yml` and validates against *Christian Hermann's*
  minisign public key, which this fork does not and never will hold. 0.8.x builds have no
  updater at all. If you have an old copy, uninstall it and install a zip from here.
- **Settings do not migrate across the rename.** In .NET, user settings storage is tied to the
  binary identity. Because the executable is now `SimpleDnsCryptPlus.exe`, window position and
  similar preferences start fresh. Your `dnscrypt-proxy.toml` and rule files are unaffected —
  they live next to the executable and keep their original names.
- **Translations are frozen.** The old POEditor project is owned by upstream's author.
  `Resources/Translation.*.resx` in this repository is now the source of truth, and new strings
  ship English-only. See [translations/README.md](translations).
- **Portable zip only, for now.** The MSI project is kept in-tree but built by no workflow; it
  uses WiX v3, which cannot be built by `dotnet build` and whose successor (v4+) is a rewrite.
  If you used an MSI from any earlier project, uninstall it first — a portable copy and an
  MSI copy of this app manage the *same* Windows service and will conflict.
- **This app requires administrator rights** and will show a UAC prompt on every launch.

## Download

Portable, self-contained (no .NET installation needed):

- `SimpleDNSCryptPlus-<version>-x64-portable.zip`
- `SimpleDNSCryptPlus-<version>-x86-portable.zip`

Grab them from [Releases](https://github.com/xardyx2/SimpleDnsCryptPlus/releases/latest).
Each release carries `SHA256SUMS.txt` and a `.zip.minisig`. Verify with:

```powershell
Get-FileHash .\SimpleDNSCryptPlus-1.0.0-x64-portable.zip -Algorithm SHA256
minisign -Vm .\SimpleDNSCryptPlus-1.0.0-x64-portable.zip -p keys\update.pub
```

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

Before testing changes that touch live DNS, read [docs/testing/dns-safety.md](docs). The app
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

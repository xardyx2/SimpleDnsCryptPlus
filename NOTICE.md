# NOTICE

Simple DNSCrypt Plus
Copyright © 2015 - 2020 Christian Hermann
Copyright © 2021 - 2023 instant.sc
Copyright © 2026 Esperion

This product includes software developed by:

- **Christian Hermann (bitbeans)** — the original Simple DNSCrypt and the `DnsCrypt.Stamps`,
  `DnsCrypt.Blacklist` and `DnsCrypt.Toolbox` libraries that Simple DNSCrypt depended on.
  Licensed under the MIT License. The bulk of this codebase, including the entire
  `SimpleDnsCrypt` project structure, originates from that work.
- **instant.sc** — the migration from .NET Framework 4.8 to modern .NET, the MahApps.Metro 2.x
  style conversion, the in-repo `SimpleDnsCrypt.Utils` reimplementation that removed the
  dependency on the abandoned `libsodium-net` package, the `Tests` project, and the WiX
  installer source, none of which exist upstream. Licensed under the MIT License.
- **Frank Denis (jedisct1)** — [dnscrypt-proxy](https://github.com/jedisct1/dnscrypt-proxy), the
  DNS proxy this tool configures, and the
  [dnscrypt-resolvers](https://github.com/DNSCrypt/dnscrypt-resolvers) list. dnscrypt-proxy is
  released under the ISC license; its `LICENSE` ships alongside it in
  `SimpleDnsCrypt/dnscrypt-proxy/`.

Third-party libraries and their licenses are listed in
`SimpleDnsCrypt/Resources/Licenses/`.

## Relationship to the original project

This is an independent fork. It is not affiliated with, endorsed by, or maintained by
Christian Hermann, instant.sc, or the DNSCrypt organization on GitHub. The names and logos of
those parties are not used to suggest otherwise.

The minisign public key used to sign this project's release artifacts is **not** Christian
Hermann's key (`RWTSM+4BNNvkZPNkHgE88ETlhWa+0HDzU5CN8TvbyvmhVUcr6aQXfssV`). Signatures made with
that key are rejected. See `tools/keys/update.pub` for the key actually in use here.

Windows code signing certificates issued to "Christian Hermann" (COMODO RSA Code Signing CA)
mentioned in the upstream README do not apply to any artifact published from this repository.

# Security Policy

## What this application does to your system

Simple DNSCrypt Plus configures [dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy), a
local DNS proxy. It is not a network egress filter, a VPN, or an anonymizer.

Running it requires **administrator** rights, and it performs these system changes:

| Change | Where | Reversible by |
|---|---|---|
| Installs a Windows service named `dnscrypt-proxy` | SCM | `Uninstall.exe`, or `dnscrypt-proxy -uninstall` |
| Rewrites per-interface DNS server addresses to `127.0.0.1` / `::1` | `HKLM\SYSTEM\...\Network` | Set the interface back to DHCP/default |
| Writes configuration and logs next to the executable | `<install dir>\dnscrypt-proxy\` | Delete the folder |

Because it changes DNS before any name resolution can confirm the change worked, a misconfigured
proxy can make the machine appear to have no internet even while the network is fine. Recovery:

```powershell
Set-DnsClientServerAddress -InterfaceAlias "Ethernet" -ServerAddresses ("8.8.8.8","9.9.9.9")
Stop-Service dnscrypt-proxy -Force
sc.exe delete dnscrypt-proxy
Clear-DnsClientCache
```

## Signed vs unsigned artifacts

Release binaries are **not** Authenticode-signed. This project deliberately does not purchase a
code-signing certificate. Integrity is established instead by:

- `SHA256SUMS.txt` published with every release,
- a detached [minisign](https://github.com/jedisct1/minisign) signature per archive, verified
  against [`tools/keys/update.pub`](tools/keys/update.pub), and
- for the bundled `dnscrypt-proxy` binaries, SHA-256 digests pinned in
  `tools/dnscrypt-proxy.lock.json` and enforced at build time — a release build fails closed if a
  downloaded binary does not match.

The public key in `tools/keys/update.pub` is **this project's only trust anchor**. Christian
Hermann's key (`RWTSM+4BNNvkZPNkHgE88ETlhWa+0HDzU5CN8TvbyvmhVUcr6aQXfssV`), which appears in the
upstream project's history and in old installed copies, is not trusted here and signatures made
with it are rejected.

The in-app updater fetches its manifest over HTTPS from this repository's own GitHub Releases and
verifies the payload's minisign signature inline before writing anything to disk. It never
performs an in-place self-replacement of the running executable.

## Known limitations

- Unsigned binaries attract SmartScreen warnings and antivirus false positives. See
  `docs/AV-FALSE-POSITIVES.md`. These are not treated as vulnerabilities and will not be "fixed"
  by signing, because no certificate is planned.
- Translations are frozen; see `translations/README.md`.

## Reporting a vulnerability

Open a [GitHub security advisory](https://github.com/xardyx2/SimpleDnsCryptPlus/security/advisories/new)
rather than a public issue. Expect an acknowledgement within a week; this is maintained by a small
team in spare time, so please allow reasonable time for a fix. Do not file a public issue for an
unresolved remote-code-execution or DNS-hijacking problem.

## Scope

Supported: the current release and the previous one, on the two most recent Windows versions.
Not supported: Windows 7/8 (the original project supported them; this one targets .NET 10, which
does not), and any build of upstream Simple DNSCrypt or instant.sc's fork — report those to their
own repositories.

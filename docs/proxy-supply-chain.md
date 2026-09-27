# dnscrypt-proxy binary supply chain

Simple DNSCrypt Plus does not work without the `dnscrypt-proxy` executable, and that executable is
written in Go by a different project. Until this fork, both builds of it were committed to git as
~22 MB of opaque bytes with no record of where they came from. They are no longer committed.

## How the binaries arrive

```
tools/dnscrypt-proxy.lock.json   pinned version + SHA-256 per architecture
        |
        v
build/fetch-proxy.ps1            download -> verify digest -> extract -> copy
        |
        v
SimpleDnsCrypt/dnscrypt-proxy/dnscrypt-proxy{64,86}.exe   (git-ignored)
```

`SimpleDnsCrypt.csproj` has an `EnsureDnscryptProxyBinaries` target that runs the script before the
first build if the executables are absent, so a fresh clone needs no manual setup step. For an
offline or source-only build, pass `-p:SkipDnscryptProxyDownload=true`; the `<Content>` items are
conditional on the files existing, so the build then succeeds without them and the app reports
missing files at startup, as intended.

## Why a pinned digest rather than a signature check

Upstream publishes `dnscrypt-proxy-win64-2.1.18.zip.minisig` alongside each archive, which is the
right thing to verify. It is not, however, turnkey in a build pipeline: the public key it must be
checked against is **not** in the `DNSCrypt/dnscrypt-proxy` repository — there is no `minisign.pub`
anywhere in its tree — so a workflow would have to obtain the key from a website, which is the same
trust problem one layer up.

So the automated control is a SHA-256 committed to this repository. It is auditable in git history,
it fails closed, and it is not affected by how the key is distributed. Signature verification is a
**manual pre-release step** performed by a human, documented below.

## Updating to a new dnscrypt-proxy release

1. Read the release notes at <https://github.com/DNSCrypt/dnscrypt-proxy/releases>.
2. Download the candidate archives **and their `.minisig` files**:
   ```powershell
   $v = "2.1.18"
   foreach ($a in "win64","win32") {
     Invoke-WebRequest "https://github.com/DNSCrypt/dnscrypt-proxy/releases/download/$v/dnscrypt-proxy-$a-$v.zip" -OutFile "dnscrypt-proxy-$a-$v.zip"
     Invoke-WebRequest "https://github.com/DNSCrypt/dnscrypt-proxy/releases/download/$v/dnscrypt-proxy-$a-$v.zip.minisig" -OutFile "dnscrypt-proxy-$a-$v.zip.minisig"
   }
   ```
3. Verify each signature with [minisign](https://github.com/jedisct1/minisign) and the key Frank
   Denis publishes on <https://download.dnscrypt.info>. Confirm the key fingerprint out of band
   before trusting a release because of it:
   ```
   minisign -Vm dnscrypt-proxy-win64-2.1.18.zip -p <minisign.pub>
   ```
   The trusted comment should name the same file and say `hashed`.
4. Compute the digests and put them in `tools/dnscrypt-proxy.lock.json`, updating `version` and
   `releaseUrl` in the same commit:
   ```powershell
   (Get-FileHash .\dnscrypt-proxy-win64-2.1.18.zip -Algorithm SHA256).Hash.ToLower()
   ```
5. `Remove-Item SimpleDnsCrypt\dnscrypt-proxy\dnscrypt-proxy*.exe` so the fetch really re-runs,
   then `dotnet build -c Release`.
6. Confirm the shipped binary reports the version you pinned:
   `.\SimpleDnsCrypt\dnscrypt-proxy\dnscrypt-proxy64.exe -version`

A release that has not been signature-verified should still not be merged if step 3 fails; the
digest only prevents silent substitution of a file you already intended to use.

## What this does not protect

- It does not make upstream trustworthy; it makes them accountable. If a bad 2.1.19 is published
  and someone pins it here, the digest matches and the build passes.
- It does not cover the `example-*.txt`, `LICENSE` or `dnscrypt-proxy.toml.example` files, which
  are still committed to this repository and updated by hand.
- A contributor with write access to this repository can change the lock file. Reviewing that diff
  is the control; CI cannot tell a legitimate bump from a hostile one.

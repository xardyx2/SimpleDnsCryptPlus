# Antivirus and SmartScreen warnings: what is going on

Upstream tracks this as [issue #574](https://github.com/DNSCrypt/SimpleDnsCrypt/issues/574). It is not
closed, it will not be closed by this fork, and anyone who tells you otherwise is selling something.

## What this program does that heuristics dislike

None of it is malicious, all of it is what a heuristic is trained to flag:

| Behaviour | Why the app does it |
|---|---|
| Writes DNS server lists per interface under `HKLM\SYSTEM\...\Network` | That is how a system DNS proxy is enabled. Redirecting DNS is also the classic malware persistence move. |
| Installs and controls a Windows service named `dnscrypt-proxy` | So resolution survives logout and reboot. |
| Ships a second executable (`dnscrypt-proxy64.exe`, ~12 MB, Go) | The proxy itself. Go binaries have high entropy and unusual section layout, which is a standing false-positive trigger. |
| Runs elevated by default (`requireAdministrator`) | UAC on every launch, which reads as suspicious to users and to some telemetry. |
| Is delivered as an unsigned zip | No Authenticode. Extracted files inherit Mark-of-the-Web. |
| Phone-homes to exactly one URL on a code-signing-adjacent domain | The update check. It is documented, opt-out, and verifies what it downloads — and it still looks like a dropper from the outside. |

The publisher account is new, so SmartScreen has no download reputation for it, and reputation is
keyed to file hash: **a rebuild of identical source produces a new hash and resets whatever reputation
the old file had.** That is one reason CI publishes from tags rather than from every push.

## What this project does about it

- Every release asset ships with a SHA-256 in `SHA256SUMS.txt` and a detached minisign signature.
  The public key is in-repo (`tools/keys/update.pub`) and compiled into the app, so the update channel
  cannot be poisoned by a swapped asset.
- The bundled `dnscrypt-proxy` is not committed to git. It is fetched at build time and its SHA-256 is
  compared against `tools/dnscrypt-proxy.lock.json`, failing the build on mismatch — see
  `docs/proxy-supply-chain.md`.
- No packers, no cryptors, no obfuscation, no self-extracting tricks. Nothing in the zip is trying to
  hide from a scanner.
- `AssemblyName` and artifact naming are stable across releases, so reputation and allow-list rules
  accumulate instead of resetting.
- Behaviours are documented here and in `SECURITY.md` rather than discovered by a user in a forum.

## What it will not do

Buy a code-signing certificate. This project takes no donations and has no legal entity to buy one as;
an Extended Validation certificate would be a recurring personal expense for whoever paid it, and a
basic OV certificate does not silence SmartScreen either — it changes whose reputation is on the hook,
which is not the same as having any.

## If you hit a warning

1. Check the digest against `SHA256SUMS.txt` for the release you downloaded. Do this before anything
   else; it is the check that actually matters.
2. Submit the file to your vendor as a false positive:
   - Microsoft: <https://www.microsoft.com/wdsi/filesubmission> (choose "False positive")
   - The general index at <https://www.virustotal.com> lets you see which engine objects.
   Tell us the engine and version in a project issue; we will track the submission alongside the
   release, and the report from a signed-in account is far more likely to be actioned than a forum post.
3. If you need it working now, add an exclusion for the **folder you installed into**, not for
   "dnscrypt" as a pattern, and not for real-time protection globally.
4. Do not download from a mirror because a scanner complained. The complaint is the reason to verify
   the hash, and the only authoritative sources are this repository's releases and
   `dnscrypt-proxy`'s own.

## What we ask of you

Do not treat a scanner verdict as a vulnerability report, and do not open an issue that says "this is
malware, it tripped Defender" without a hash check behind it. Do open an issue if a *specific* engine
version flags a *specific* release, with the SHA-256 of what you downloaded — that is the version of
this report we can act on.

# Testing safely when the software changes your DNS

Simple DNSCrypt Plus installs a Windows service and rewrites the DNS server list on network
interfaces. Done carelessly, that leaves the test machine with no name resolution at all — and since
`git`, `gh`, `dotnet restore` and web browsing all need DNS, it also leaves you unable to look up how
to fix it.

Pick a level before you start, and stay in it.

## R0 — read-only. Default for every change.

Run the app and look at it. Do not click **Install service**, **Start service**, or apply anything on
the Route tab.

This is enough to verify all of: window chrome and titles, the ten tabs and their view resolution,
language switching and satellite resources, log views, drag-drop ordering, the update dialog, and
every code path that only reads files.

What the app does write at R0, by design: `dnscrypt-proxy/dnscrypt-proxy.toml` and
`dnscrypt-proxy/configVersion.txt` **inside its own folder**. Nothing outside the folder is touched.
A first launch therefore cannot break your network.

## R1 — the proxy, on localhost, as an ordinary user

Exercises the real dnscrypt-proxy binary and produces real log volume (which is how you reproduce the
memory-growth issues) with zero system change:

```powershell
cd <unzipped folder>\dnscrypt-proxy
.\dnscrypt-proxy64.exe --test-config        # validates the config, changes nothing

# edit a COPY of the toml, then:
#   listen_addresses = ['127.0.0.1:5354']
.\dnscrypt-proxy64.exe -c .\dnscrypt-proxy.toml    # not port 53, no service, no admin
Resolve-DnsName www.example.com -Server 127.0.0.1 -Port 5354
```

Run the proxy from the app's own `dnscrypt-proxy\` folder with the app's own toml, otherwise the log
paths the app tails will not match the paths the proxy writes and the Query log will look empty.

## R2 — real service, real DNS change. Sandboxes only.

Windows Sandbox or a VM with a checkpoint. Never your working machine's primary adapter.

**In a VM, set `SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS=1` before launching the app.** The interface list is
filtered against a blacklist of virtual, tunnel and loopback adapter names, and a guest's adapters are
all on that list — without the variable the app shows zero interfaces and looks broken when nothing is
wrong. (`Global.NetworkInterfaceBlacklist`, filtered in `LocalNetworkInterfaceManager`.)

Before you touch anything, in a **second elevated PowerShell window you leave open**:

```powershell
Get-DnsClientServerAddress | Tee-Object dns-backup.txt
ipconfig /all > pre.txt
Get-Service dnscrypt-proxy
netsh interface ipv4 show dnsservers
```

**Type the recovery commands somewhere you can see them before clicking anything.** Do not wait to
need them, and do not rely on a GUI:

```powershell
Set-DnsClientServerAddress -InterfaceAlias "Ethernet" -ServerAddresses ("8.8.8.8","9.9.9.9")
Stop-Service dnscrypt-proxy -Force -ErrorAction SilentlyContinue
sc.exe delete dnscrypt-proxy
Clear-DnsClientCache
```

The failure mode these are for: the proxy is set as the interface DNS server and then stops or
crashes. Every lookup on the machine fails until you run the `Set-DnsClientServerAddress` line.
Windows may flap "no internet" while that is true — upstream issue #533, reproducing it here is useful
signal, not a new emergency. If IPv6 was also configured, fix both families.

Prefer a secondary adapter over the Wi-Fi you work on. Do not enable "hide all network interfaces"
mid-test. Do not do R2 with uncommitted work you would mind losing.

## What Uninstall.exe does and does not do

It is the only cleanup path that ships in the portable zip (there is no MSI — see
`docs/adr/0002-portable-only-no-msi.md`). Read `Uninstall/Program.cs` before trusting it:

- For every operational, non-blacklisted interface it runs
  `netsh interface ipv4 delete dns "<name>" all` and the `ipv6` equivalent. That removes the static
  DNS entries, so the interface goes back to whatever DHCP offers.
- It **does not stop or remove the `dnscrypt-proxy` service.** For that use the app's own uninstall
  button or `dnscrypt-proxy -uninstall`.
- Every failure inside it is swallowed. Check with `Get-DnsClientServerAddress` afterwards; do not
  assume.

The updater never needs any of this. A staged update lands in `_update\<version>\` beside the app and
is unpacked only after its hash and signature check out; it does not touch DNS, the service, or the
running executable.

## Why the app demands administrator, and what that costs you as a tester

`SimpleDnsCrypt/Properties/app.manifest` requests `requireAdministrator`, so every launch shows UAC —
which is also why some users arrive convinced it is malware (see `docs/AV-FALSE-POSITIVES.md`).

A practical consequence for automated testing: UI Access Isolation means a non-elevated process cannot
send synthetic input to the elevated window. Screenshots work; scripted clicking does not. Visual
passes have to be done by a person, and a headless agent should say so rather than imply it looked.

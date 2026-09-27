"""Mint the MSI's stable identifiers, and pin them into Installer/Product.wxs.

Windows Installer keys upgrades off UpgradeCode and repairs/patches off component GUIDs, so both
have to be constant for the life of the product and unique against every predecessor. Typing them
from memory is how a project ends up accidentally reusing instant.sc's UpgradeCode - the exact
hazard docs/adr/0002 warns about - so they are derived here instead: a name-based (v5) UUID over
SHA-1 of a label, the same construction RFC 4122 defines, with the version and variant nibbles
forced the way it requires.

Run with:  python tools/mint-upgradecode.py          # report what the .wxs should contain
Run with:  python tools/mint-upgradecode.py --write  # rewrite the .wxs to match
"""
import hashlib
import re
import sys
from pathlib import Path
from uuid import UUID

# Each identifier a release forever keeps. Changing a label here changes the identity of every
# future upgrade, so labels are versioned strings, never edited in place.
IDENTIFIERS = {
    "msi/upgrade-code": "UpgradeCode",
    "msi/component/uninstall-helper": None,
    "msi/component/start-menu-shortcut": None,
    "msi/component/service-cleanup": None,
}

WXS = Path(__file__).resolve().parent.parent / "Installer" / "Product.wxs"


def mint(label: str) -> str:
    digest = hashlib.sha1(f"esperion:simplednscryptplus:{label}".encode()).digest()[:16]
    value = int.from_bytes(digest, "big")
    # In the 32 hex digits of a UUID, the version is digit 13 and the variant is digit 17 - counted
    # from the left, so from bit 76 and bit 60 of the integer. Masking 0xF000 outright sets the
    # wrong nibble and silently produces something that is not a v5 UUID.
    value = (value & ~(0xF << 76)) | (0x5 << 76)          # version 5: name-based
    value = (value & ~(0x3 << 62)) | (0x2 << 62)          # variant bits 10xx, per RFC 4122
    minted = str(UUID(int=value))

    parts = minted.split("-")
    assert parts[2][0] == "5", f"{label}: version nibble is not 5 ({minted})"
    assert parts[3][0] in "89ab", f"{label}: variant nibble is not RFC 4122 ({minted})"
    return minted


def main() -> int:
    wanted = {label: mint(label) for label in IDENTIFIERS}

    for label, guid in wanted.items():
        print(f"{label:36} -> {guid}")

    if "--write" not in sys.argv:
        print("\nread-only; pass --write to update Installer/Product.wxs")
        return 0

    text = WXS.read_text(encoding="utf-8")

    # A GUID is only ever replaced where it is used, never where it is explained: the docs/adr text
    # names instant.sc's UpgradeCode deliberately, and that must survive untouched. The label
    # comment is held in group 1 and re-emitted, so running this twice is a no-op.
    pattern = re.compile(
        r"(?P<pre><!--\s*label:\s*(?P<label>[a-z0-9/-]+)\s*-->\s*\n\s*<[A-Za-z]+[^>]*?)"
        r'Guid="(?P<guid>[0-9a-fA-F-]{36})"',
        re.MULTILINE,
    )

    found = [m.group("label") for m in pattern.finditer(text)]
    expected = [label for label in IDENTIFIERS if label != "msi/upgrade-code"]
    if sorted(found) != sorted(expected):
        raise SystemExit(
            f"{WXS.name}: labelled component GUIDs do not line up\n"
            f"  expected: {sorted(expected)}\n"
            f"  found   : {sorted(found)}"
        )

    def replace(match: re.Match) -> str:
        label = match.group("label").strip()
        if label not in wanted:
            raise SystemExit(f"{WXS.name}: unknown identifier label in comment: {label!r}")
        return f'{match.group("pre")}Guid="{wanted[label]}"'

    text = pattern.sub(replace, text)

    upgrade_line = re.compile(r'UpgradeCode="(?P<guid>[0-9a-fA-F-]{36})"')
    if not upgrade_line.search(text):
        raise SystemExit(f"{WXS.name}: no UpgradeCode attribute found to pin")
    text = upgrade_line.sub(lambda m: f'UpgradeCode="{wanted["msi/upgrade-code"]}"', text)

    WXS.write_text(text, encoding="utf-8", newline="\n")
    print(f"\nwrote {WXS}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

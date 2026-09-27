using System;
using NUnit.Framework;
using SimpleDnsCrypt.Helper;

namespace Tests
{
    /// <summary>
    /// The hidden-interface filter exists to keep loopback, ISATAP and tunnel adapters out of a
    /// user's list. Its side effect is that a virtual machine is built entirely out of blacklisted
    /// descriptions, so the application shows zero interfaces inside the only environment where
    /// changing live DNS settings is safe to test. The escape hatch is what makes that testable.
    /// </summary>
    public class NetworkInterfaceFilterTests
    {
        [Test]
        public void AVirtualAdapterIsHiddenByDefault()
        {
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "VMware Virtual Ethernet Adapter #0", "Ethernet 2", allowVirtual: false));
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "VirtualBox Host-Only Ethernet Adapter", "VirtualBox Host-only", allowVirtual: false));
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "TAP-Windows Adapter V9", "tap0", allowVirtual: false));
        }

        [Test]
        public void ABlacklistedNameCountsEvenWhenTheDescriptionDoesNot()
        {
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "Some vendor adapter", "Microsoft ISATAP Tunnel Adapter", allowVirtual: false));
        }

        [Test]
        public void TheGermanAdapterNamesAreFilteredToo()
        {
            // The blacklist carries German spellings because the original author localised them.
            // A user on a German guest would otherwise see adapters an English user never sees.
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "Virtueller Microsoft-Adapter für ISATAP", "ISATAP-Adapter 2", allowVirtual: false));
        }

        [Test]
        public void APhysicalInterfaceIsNeverFiltered()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "Intel(R) Ethernet Connection (17) I219-V", "Ethernet", allowVirtual: false));
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "Realtek 8822CE Wireless LAN", "Wi-Fi", allowVirtual: false));
        }

        [Test]
        public void TheSameAdapterIsListedWhenVirtualInterfacesAreAllowed()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "VMware Virtual Ethernet Adapter #0", "Ethernet 2", allowVirtual: true));
        }

        [Test]
        public void MissingOrNullInterfaceTextIsNotTreatedAsABlacklistHit()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(null, null, allowVirtual: false));
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut("", "  ", allowVirtual: false));
        }

        [Test]
        public void TheEnvironmentVariableNamedInTheDocsIsTheOneThatIsRead()
        {
            const string Name = "SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS";

            var before = Environment.GetEnvironmentVariable(Name);

            try
            {
                Environment.SetEnvironmentVariable(Name, null);
                Assert.IsFalse(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces,
                    "off unless the variable says otherwise");

                Environment.SetEnvironmentVariable(Name, "1");
                Assert.IsTrue(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces);

                Environment.SetEnvironmentVariable(Name, "0");
                Assert.IsFalse(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces,
                    "only 1 turns it on, so a stray value cannot silently widen the list");
            }
            finally
            {
                Environment.SetEnvironmentVariable(Name, before);
            }
        }
    }
}

using NUnit.Framework;
using SimpleDnsCrypt.Utils;
using SimpleDnsCrypt.Utils.Models;

namespace Tests
{
    public class StampDecodeTests
    {
        [Test]
        public void StampDecodeTest1()
        {
            const string stamp = "sdns://AQcAAAAAAAAADjIxMi40Ny4yMjguMTM2IOgBuE6mBr-wusDOQ0RbsV66ZLAvo8SqMa4QY2oHkDJNHzIuZG5zY3J5cHQtY2VydC5mci5kbnNjcnlwdC5vcmc";
            var result = StampTools.Decode(stamp);
            Assert.AreEqual(StampProtocol.DnsCrypt, result.Protocol);
            Assert.AreEqual("2.dnscrypt-cert.fr.dnscrypt.org", result.ProviderName);
        }

        [Test]
        public void StampDecodeTest2()
        {
            const string stamp = "sdns://AgcAAAAAAAAADTM3LjU5LjIzOC4yMTMgwzRA_TfjYt0RwSHqBHwj7OM-D_x-CDgqIHeJHIoN1P0UZG9oLmZyLmRuc2NyeXB0LmluZm8KL2Rucy1xdWVyeQ";
            var result = StampTools.Decode(stamp);
            Assert.AreEqual(StampProtocol.DoH, result.Protocol);
            Assert.AreEqual("doh.fr.dnscrypt.info", result.Hostname);
        }

        [Test]
        public void StampDecodeTest3()
        {
            const string stamp = "sdns://AgUAAAAAAAAAACDyXGrcc5eNecJ8nomJCJ-q6eCLTEn6bHic0hWGUwYQaA5kbnMuZ29vZ2xlLmNvbQ0vZXhwZXJpbWVudGFs";
            var result = StampTools.Decode(stamp);
            Assert.AreEqual(StampProtocol.DoH, result.Protocol);
            Assert.AreEqual("dns.google.com", result.Hostname);
        }

        [Test]
        public void StampDecodeTestRelay()
        {
            const string stamp = "sdns://gQ84My43Ny44NS43Ojg0NDM";
            var result = StampTools.Decode(stamp);
            Assert.AreEqual(StampProtocol.DNSCryptRelay, result.Protocol);
            Assert.AreEqual("83.77.85.7", result.Address);
        }

    }
}

using NUnit.Framework;
using SimpleDnsCrypt.Utils;
using SimpleDnsCrypt.Utils.Models;

namespace Tests
{
    public class StampEncodeTests
    {
        [Test]
        public void StampEncodeTest1()
        {
            var stampObject = new Stamp
            {
                Protocol = StampProtocol.DnsCrypt,
                ProviderName = "2.dnscrypt-cert.fr.dnscrypt.org",
                PublicKey = "e801b84ea606bfb0bac0ce43445bb15eba64b02fa3c4aa31ae10636a0790324d",
                Address = "212.47.228.136",
                Properties = new StampProperties
                {
                    DnsSec = true,
                    NoFilter = true,
                    NoLog = true
                }
            };
            var result = StampTools.Encode(stampObject);
            const string stamp = "sdns://AQcAAAAAAAAADjIxMi40Ny4yMjguMTM2IOgBuE6mBr-wusDOQ0RbsV66ZLAvo8SqMa4QY2oHkDJNHzIuZG5zY3J5cHQtY2VydC5mci5kbnNjcnlwdC5vcmc";
            Assert.AreEqual(stamp, result);
        }
    }
}

using System.Collections.Generic;

namespace SimpleDnsCrypt.Utils.Models
{
    public class Stamp
    {
        public StampProtocol Protocol { get; set; }
        public StampProperties Properties { get; set; }
        public string Address { get; set; }
        public string PublicKey { get; set; }
        public string ProviderName { get; set; }
        public string Hash { get; set; }
        public string Hostname { get; set; }
        public string Path { get; set; }
        public int Port { get; set; }

        public IEnumerable<string> ValidationIssues
        {
            get
            {
                if (Protocol == StampProtocol.DnsCrypt)
                {
                    if (string.IsNullOrEmpty(Address))
                    {
                        yield return "Empty address";
                    }

                    if (string.IsNullOrEmpty(PublicKey))
                    {
                        yield return "Empty public key";
                    }

                    yield break;
                }

                if (Protocol == StampProtocol.DoH)
                {
                    if (string.IsNullOrEmpty(Hash))
                    {
                        yield return "Empty hash";
                    }

                    if (string.IsNullOrEmpty(Hostname))
                    {
                        yield return "Empty hostname";
                    }

                    yield break;
                }

                if (Protocol == StampProtocol.DNSCryptRelay)
                {
                    yield break;
                }

                yield return $"Unsupported protocol {Protocol}. For now, only {nameof(StampProtocol.DnsCrypt)}, " +
                             $"{nameof(StampProtocol.DoH)} and {StampProtocol.DNSCryptRelay} are supported";
            }
        }
    }
}

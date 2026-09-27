using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// The config migration in PatchHelper replaces the public-resolvers and relays source URLs
    /// when a pre-existing configuration is first loaded. Upstream only rewrote a list whose URLs
    /// still pointed at the old /v2/ defaults; the current code rewrites unconditionally, which
    /// silently destroys a user's own resolver list. These tests pin the conservative rule.
    /// </summary>
    public class ResolverListMigrationTests
    {
        private static readonly string[] DefaultV2 =
        {
            "https://raw.githubusercontent.com/DNSCrypt/dnscrypt-resolvers/master/v2/public-resolvers.md",
            "https://download.dnscrypt.info/resolvers-list/v2/public-resolvers.md"
        };

        private static readonly string[] DefaultV3 =
        {
            "https://raw.githubusercontent.com/DNSCrypt/dnscrypt-resolvers/master/v3/public-resolvers.md",
            "https://download.dnscrypt.info/resolvers-list/v3/public-resolvers.md",
            "https://ipv6.download.dnscrypt.info/resolvers-list/v3/public-resolvers.md"
        };

        [Test]
        public void UntouchedDefaultV2List_IsRewritten()
        {
            Assert.IsTrue(ResolverListMigration.IsOutdatedDefaultList(DefaultV2));
        }

        [Test]
        public void CurrentV3List_IsLeftAlone()
        {
            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(DefaultV3));
        }

        [Test]
        public void CustomThirdPartyList_IsLeftAlone()
        {
            var custom = new[]
            {
                "https://example.com/my-resolvers.md",
                "https://mirror.example.com/my-resolvers.md"
            };

            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(custom));
        }

        [Test]
        public void PartiallyCustomisedList_IsLeftAlone()
        {
            // user replaced only the second URL: rewriting both would discard their choice
            var halfCustom = new[] { DefaultV2[0], "https://example.com/mine.md" };

            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(halfCustom));
        }

        [Test]
        public void SingleUrlList_IsLeftAloneWithoutThrowing()
        {
            // the shipped upstream check indexed urls[1] unguarded and would throw here
            var single = new[] { DefaultV2[0] };

            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(single));
        }

        [Test]
        public void EmptyList_IsLeftAlone()
        {
            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(new string[0]));
        }

        [Test]
        public void NullList_IsLeftAlone()
        {
            Assert.IsFalse(ResolverListMigration.IsOutdatedDefaultList(null));
        }
    }
}

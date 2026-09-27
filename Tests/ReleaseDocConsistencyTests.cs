using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// The README's dnscrypt-proxy badge is the only place the shipped proxy version is stated to
    /// users, and it is prose: nothing in the build reads it. It sat on 2.1.5 long after
    /// tools/dnscrypt-proxy.lock.json moved to 2.1.18, so the front page advertised a proxy older
    /// than every binary the fork published.
    ///
    /// This test reads the source rather than the built app on purpose, matching
    /// <see cref="AssemblyNameConsistencyTests"/>: the failure mode is a document that no longer
    /// agrees with the value the build actually consumes.
    /// </summary>
    public class ReleaseDocConsistencyTests
    {
        // img.shields.io/badge/dnscrypt--proxy-2.1.18-orange.svg - the doubled hyphen is shields'
        // escape for a literal hyphen in the label, so the version is what follows it.
        private static readonly Regex ProxyBadge =
            new Regex("img\\.shields\\.io/badge/dnscrypt--proxy-(?<v>[0-9][0-9.]*)", RegexOptions.Compiled);

        private const string LockFileName = "dnscrypt-proxy.lock.json";

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleDnsCrypt.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "could not locate the solution root from " + AppContext.BaseDirectory);

            return dir.FullName;
        }

        private static string Readme()
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
        }

        private static string PinnedProxyVersion()
        {
            var path = Path.Combine(RepoRoot(), "tools", LockFileName);
            Assert.IsTrue(File.Exists(path), $"tools/{LockFileName} is missing");

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            Assert.IsTrue(document.RootElement.TryGetProperty("version", out var version),
                $"tools/{LockFileName} has no \"version\" property; if it was renamed, this guard must follow");

            return version.GetString();
        }

        [Test]
        public void TheReadmeBadgeMatchesThePinnedProxyVersion()
        {
            var match = ProxyBadge.Match(Readme());

            Assert.IsTrue(match.Success,
                $"README no longer carries a shields.io dnscrypt-proxy badge - update this guard");

            Assert.AreEqual(PinnedProxyVersion(), match.Groups["v"].Value,
                "README advertises a different bundled dnscrypt-proxy than the one build/fetch-proxy.ps1 " +
                "pins and the portable zip ships");
        }

        [Test]
        public void TheReadmeCarriesExactlyOneProxyBadge()
        {
            // The comparison above only checks the first match. If a second badge is ever added -
            // or this one is moved into a section that stops being the header - the guard would be
            // quietly reading the wrong line, so pin the count the way the markup guard does.
            var total = ProxyBadge.Matches(Readme()).Count;

            Assert.AreEqual(1, total,
                "expected exactly 1 dnscrypt-proxy badge in README.md; the guard's coverage changed");
        }
    }
}

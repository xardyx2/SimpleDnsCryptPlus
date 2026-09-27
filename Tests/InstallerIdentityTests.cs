using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// The MSI is the one artifact in this release that a user cannot inspect before running it, and
    /// its identity is load-bearing in a way the zip's is not: a package carrying instant.sc's
    /// UpgradeCode would silently replace, or be replaced by, an install this project does not control,
    /// and AllowDowngrades="yes" would let an older proxy overwrite a newer one. docs/adr/0002 lists
    /// both hazards, and nothing in the build enforced either one.
    ///
    /// So this reads the WiX source rather than the built MSI: the failures it looks for are ones a
    /// future edit introduces, and by then the package may not exist yet. The same checks run against a
    /// canary carrying the exact bad values, which is what proves the assertions can fail at all -
    /// see <see cref="TheGuardsAreNotVacuous"/>.
    /// </summary>
    public class InstallerIdentityTests
    {
        private static readonly XNamespace Wix = "http://schemas.microsoft.com/wix/2006/wi";

        // instant.sc's UpgradeCode. Naming it here is deliberate: the guard below fails if it ever
        // turns up in the package identity, and this comment is why that string exists in a file that
        // is scanned for it.
        private const string PredecessorUpgradeCode = "b561df39-7e27-44e4-978d-22df6eea11b4";

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

        private static string ProductWxs()
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "Installer", "Product.wxs"));
        }

        /// <summary>
        /// Every identity rule the MSI must satisfy, as violations rather than passes, so the same
        /// list can be run against a known-bad document.
        /// </summary>
        private static List<string> IdentityViolations(string wxsSource)
        {
            var problems = new List<string>();

            XDocument document;
            try
            {
                document = XDocument.Parse(wxsSource);
            }
            catch (Exception ex)
            {
                problems.Add($"Product.wxs does not parse: {ex.GetType().Name}");
                return problems;
            }

            var product = document.Root?.Element(Wix + "Product");
            if (product == null)
            {
                problems.Add("no <Product> element");
                return problems;
            }

            var upgradeCode = (string)product.Attribute("UpgradeCode") ?? "";
            if (string.Equals(upgradeCode, PredecessorUpgradeCode, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("UpgradeCode is instant.sc's; the package would trade installs with a project we do not control");
            }
            if (!Guid.TryParse(upgradeCode, out _))
            {
                problems.Add($"UpgradeCode '{upgradeCode}' is not a GUID");
            }

            var name = (string)product.Attribute("Name") ?? "";
            var manufacturer = (string)product.Attribute("Manufacturer") ?? "";
            if (name != SimpleDnsCrypt.Config.Global.ApplicationName)
            {
                problems.Add($"Product Name '{name}' differs from Global.ApplicationName '{SimpleDnsCrypt.Config.Global.ApplicationName}'");
            }
            if (manufacturer != "Esperion")
            {
                problems.Add($"Manufacturer is '{manufacturer}', expected Esperion");
            }

            var version = (string)product.Attribute("Version") ?? "";
            if (version != "$(var.Version)")
            {
                problems.Add($"Version is '{version}'; it must come from the build, not from a literal in the file");
            }

            var package = product.Element(Wix + "Package");
            if ((string)package?.Attribute("InstallScope") != "perMachine")
            {
                problems.Add("Package InstallScope is not perMachine");
            }
            if ((string)package?.Attribute("Platform") != "$(var.Platform)")
            {
                problems.Add("Package Platform is not supplied by the build");
            }

            // Read the attribute, never the raw text: Product.wxs's header comment names this exact
            // value while explaining why it was dropped, and a substring match reported that as a
            // defect in the package.
            if (document.Descendants(Wix + "MajorUpgrade")
                    .Any(m => string.Equals((string)m.Attribute("AllowDowngrades"), "yes",
                                            StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add("AllowDowngrades=\"yes\" lets an older package replace a newer one");
            }

            var installFolders = document.Descendants(Wix + "Directory")
                .Where(d => (string)d.Attribute("Id") == "INSTALLFOLDER")
                .Select(d => (string)d.Attribute("Name"))
                .ToList();
            if (installFolders.Count != 1)
            {
                problems.Add($"expected exactly 1 INSTALLFOLDER directory, found {installFolders.Count}");
            }
            else if (installFolders[0] != "SimpleDNSCryptPlus")
            {
                problems.Add($"install directory is '{installFolders[0]}', which collides with a predecessor's folder");
            }

            var services = document.Descendants(Wix + "ServiceControl")
                .Select(s => (string)s.Attribute("Name"))
                .ToList();
            if (services.Count != 1)
            {
                problems.Add($"expected exactly 1 ServiceControl, found {services.Count}");
            }
            else if (services[0] != "dnscrypt-proxy")
            {
                problems.Add($"ServiceControl targets '{services[0]}', not the real service name dnscrypt-proxy");
            }

            var customActions = document.Descendants(Wix + "CustomAction")
                .Where(c => (string)c.Attribute("Id") == "UninstallHelper")
                .ToList();
            if (customActions.Count != 1)
            {
                problems.Add($"expected exactly 1 UninstallHelper custom action, found {customActions.Count}");
            }
            else
            {
                var action = customActions[0];
                if ((string)action.Attribute("Execute") != "deferred" || (string)action.Attribute("Impersonate") != "no")
                {
                    problems.Add("UninstallHelper must be deferred and non-impersonated: it calls netsh and needs the system context");
                }
            }

            return problems;
        }

        /// <summary>
        /// The predecessor's package, shape for shape, as the canary that the rules above must reject.
        /// </summary>
        private const string Canary = @"<Wix xmlns=""http://schemas.microsoft.com/wix/2006/wi"">
  <Product Id=""*"" Name=""SimpleDNSCrypt.insc"" Language=""1033"" Manufacturer=""instant.sc""
           Version=""0.8.2"" UpgradeCode=""b561df39-7e27-44e4-978d-22df6eea11b4"">
    <Package InstallerVersion=""200"" Compressed=""yes"" InstallScope=""perMachine"" Platform=""x64"" />
    <MajorUpgrade AllowDowngrades=""yes"" />
    <DirectoryRef Id=""TARGETDIR"">
      <Directory Id=""INSTALLFOLDER"" Name=""SimpleDNSCrypt"" />
      <Component Id=""ServiceCleanup"" Guid=""e2b3771e-5b8e-4e07-9f46-6133d09ee20b"">
        <ServiceControl Id=""Remove"" Name=""dnscrypt-proxy"" Remove=""uninstall"" />
      </Component>
    </DirectoryRef>
  </Product>
</Wix>";

        [Test]
        public void TheInstallerSourceSatisfiesEveryIdentityRule()
        {
            var violations = IdentityViolations(ProductWxs());

            Assert.IsEmpty(violations,
                "Installer/Product.wxs broke the MSI's identity:\n  " + string.Join("\n  ", violations));
        }

        [Test]
        public void ThePinnedWixDigestIsAFullSha256()
        {
            var lockPath = Path.Combine(RepoRoot(), "tools", "wix.lock.json");
            Assert.IsTrue(File.Exists(lockPath), "tools/wix.lock.json is missing");

            var json = File.ReadAllText(lockPath);
            var digest = Regex.Match(json, "\"sha256\"\\s*:\\s*\"(?<v>[^\"]+)\"");

            Assert.IsTrue(digest.Success, "tools/wix.lock.json has no \"sha256\" property");
            Assert.IsTrue(Regex.IsMatch(digest.Groups["v"].Value, "^[0-9a-f]{64}$"),
                $"pinned digest '{digest.Groups["v"].Value}' is not 64 lowercase hex characters");

            Assert.IsNotEmpty(Regex.Match(json, "\"version\"\\s*:\\s*\"(?<v>[^\"]+)\"").Groups["v"].Value,
                "the pinned WiX release has no version to fetch");
        }

        [Test]
        public void TheGuardsAreNotVacuous()
        {
            // If any rule above silently stopped matching, the real document would stay green while
            // checking nothing. Feed it the bad package it exists to catch.
            var violations = IdentityViolations(Canary);

            var mustReport = new[]
            {
                "UpgradeCode is instant.sc's",
                "differs from Global.ApplicationName",
                "expected Esperion",
                "must come from the build",
                "AllowDowngrades",
                "collides with a predecessor's folder",
                "Platform is not supplied by the build",
            };

            foreach (var expected in mustReport)
            {
                Assert.IsTrue(violations.Any(v => v.Contains(expected)),
                    "the canary package is bad and the guard did not notice: nothing reported '" + expected + "'");
            }

            // The canary is well-formed XML and names dnscrypt-proxy, so those two rules must stay
            // quiet; otherwise the guard is reporting on everything and proving nothing.
            Assert.IsFalse(violations.Any(v => v.Contains("does not parse")), "canary should parse");
            Assert.IsFalse(violations.Any(v => v.Contains("expected exactly 1 ServiceControl")),
                "canary should satisfy the ServiceControl count");
        }

        [Test]
        public void BrokenXmlIsReportedRatherThanThrownPast()
        {
            var violations = IdentityViolations("<Wix xmlns=\"http://schemas.microsoft.com/wix/2006/wi\"><Product");

            Assert.AreEqual(1, violations.Count, "a truncated .wxs must produce exactly one clear failure");
            StringAssert.Contains("does not parse", violations[0]);
        }
    }
}

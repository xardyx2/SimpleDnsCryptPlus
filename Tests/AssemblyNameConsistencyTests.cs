using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// WPFLocalizeExtension looks resources up by *assembly simple name*, not by root namespace.
    /// Renaming <AssemblyName> therefore silently breaks every localized string: 12 XAML files
    /// carry lex:ResxLocalizationProvider.DefaultAssembly and LocalizationEx passes the name as a
    /// literal. It broke once already - the build stayed clean, all existing tests stayed green,
    /// and the app simply never got past the splash dialog.
    ///
    /// This test reads the source rather than the compiled output on purpose: the failure mode is
    /// a value that no longer matches the project's assembly name, and that is only visible in
    /// the markup that will be compiled next.
    /// </summary>
    public class AssemblyNameConsistencyTests
    {
        private static readonly Regex DefaultAssembly =
            new Regex("DefaultAssembly=\"(?<v>[^\"]+)\"", RegexOptions.Compiled);

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

        private static string DeclaredAssemblyName()
        {
            var csproj = Path.Combine(RepoRoot(), "SimpleDnsCrypt", "SimpleDnsCrypt.csproj");
            var match = Regex.Match(File.ReadAllText(csproj), "<AssemblyName>(?<v>[^<]+)</AssemblyName>");

            Assert.IsTrue(match.Success,
                $"{csproj} has no <AssemblyName>; if that is intentional, this test must be updated to read the default");

            return match.Groups["v"].Value.Trim();
        }

        [Test]
        public void LocalizationExAsksForTheAssemblyTheProjectActuallyBuilds()
        {
            var expected = DeclaredAssemblyName();
            var source = File.ReadAllText(
                Path.Combine(RepoRoot(), "SimpleDnsCrypt", "Helper", "LocalizationEx.cs"));

            var match = Regex.Match(source,
                "GetLocalizedObject\\(\"(?<v>[^\"]+)\"");

            Assert.IsTrue(match.Success, "LocalizationEx no longer calls GetLocalizedObject - update this guard");

            Assert.AreEqual(expected.ToLowerInvariant(), match.Groups["v"].Value.ToLowerInvariant(),
                "LocalizationEx looks up resources in an assembly that does not exist; " +
                "GetUiString will return null and string.Format will throw");
        }

        [Test]
        public void EveryXamlLocalizationProviderNamesTheCurrentAssembly()
        {
            var expected = DeclaredAssemblyName().ToLowerInvariant();
            var stale = new List<string>();

            foreach (var file in Directory.GetFiles(
                         Path.Combine(RepoRoot(), "SimpleDnsCrypt"), "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                    file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                foreach (Match m in DefaultAssembly.Matches(File.ReadAllText(file)))
                {
                    if (m.Groups["v"].Value.Trim().ToLowerInvariant() != expected)
                    {
                        stale.Add($"{Path.GetFileName(file)} = \"{m.Groups["v"].Value}\"");
                    }
                }
            }

            Assert.IsEmpty(stale,
                "DefaultAssembly no longer matches <AssemblyName> in: " + string.Join(", ", stale));
        }

        [Test]
        public void TheMarkupGuardsAreNotVacuous()
        {
            // if a refactor ever removes every DefaultAssembly attribute, the test above would pass
            // while checking nothing. Pin the expected count so its disappearance is loud.
            var files = Directory.GetFiles(Path.Combine(RepoRoot(), "SimpleDnsCrypt"), "*.xaml",
                SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

            var total = files.Sum(f => DefaultAssembly.Matches(File.ReadAllText(f)).Count);

            Assert.AreEqual(12, total,
                "expected exactly 12 DefaultAssembly attributes; the guard's coverage changed");
        }
    }
}

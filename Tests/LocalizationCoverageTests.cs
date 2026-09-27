using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleDnsCrypt.Helper;

namespace Tests
{
    /// <summary>
    /// Guards the failure that bit us during the rename: LocalizationEx.GetUiString returns null
    /// when a culture has no resources, and five call sites pass that result straight to
    /// string.Format, which throws ArgumentNullException(Parameter 'format') and leaves the app
    /// stuck on the splash dialog. There is no neutral Translation.resx to fall back to, so a
    /// culture without a satellite file is enough to reproduce it.
    /// </summary>
    public class LocalizationCoverageTests
    {
        /// <summary>Keys used as the format argument of string.Format - a null here crashes.</summary>
        private static readonly string[] FormatKeys =
        {
            "loader_validate_folder",
            "loader_missing_files",
            "loader_loading",
            "loader_successfully_loaded",
            "loader_failed_loading"
        };

        private static IEnumerable<string> SupportedShortCodes =>
            LocalizationEx.GetSupportedLanguages().Select(l => l.ShortCode);

        [Test]
        public void EveryOfferedLanguageResolvesTheFormatStringsUsedByTheLoader()
        {
            var broken = new List<string>();

            foreach (var code in SupportedShortCodes)
            {
                var culture = new CultureInfo(code, false);

                foreach (var key in FormatKeys)
                {
                    if (LocalizationEx.GetUiString(key, culture) == null)
                    {
                        broken.Add($"{code}/{key}");
                    }
                }
            }

            Assert.IsEmpty(broken,
                "GetUiString returned null for: " + string.Join(", ", broken) +
                " - string.Format(null, ...) throws and the app never leaves the splash dialog.");
        }

        [Test]
        public void EveryOfferedLanguageHasATranslationFile()
        {
            var resources = TranslationFileCultures();
            var missing = SupportedShortCodes
                .Select(c => c.ToLowerInvariant())
                .Where(c => !resources.Contains(c))
                .ToList();

            Assert.IsEmpty(missing,
                "offered in the language dropdown but no Translation.<code>.resx exists: " +
                string.Join(", ", missing));
        }

        [Test]
        public void EveryTranslationFileIsOfferedInTheLanguageDropdown()
        {
            var offered = SupportedShortCodes.Select(c => c.ToLowerInvariant()).ToList();
            var orphaned = TranslationFileCultures()
                .Where(c => !offered.Contains(c))
                .ToList();

            Assert.IsEmpty(orphaned,
                "translation files that no user can ever select: " + string.Join(", ", orphaned));
        }

        private static HashSet<string> TranslationFileCultures()
        {
            var repoRoot = FindRepoRoot();
            var dir = Path.Combine(repoRoot, "SimpleDnsCrypt", "Resources");

            Assert.IsTrue(Directory.Exists(dir), $"translation resources not found at {dir}");

            return new HashSet<string>(
                Directory.GetFiles(dir, "Translation.*.resx")
                    .Select(f => Path.GetFileName(f).Substring("Translation.".Length,
                        Path.GetFileName(f).Length - "Translation.".Length - ".resx".Length))
                    .Select(c => c.ToLowerInvariant()));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleDnsCrypt.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "could not locate the solution root from " + AppContext.BaseDirectory);

            return dir.FullName;
        }
    }
}

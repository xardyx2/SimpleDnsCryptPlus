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
            "loader_failed_loading",
            // Added with the first Plus updater and present only in the neutral file: proof that a
            // satellite lacks a key still resolves it, which is what keeps new English-only strings
            // from crashing a German user's app.
            "updater_available",
            "updater_staged",
            "updater_failed"
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

        /// <summary>
        /// The 34 language files came out of one POEditor export and still carry an identical key set.
        /// That is the one property worth freezing: a key that reaches only some languages produces an
        /// interface that is half translated, and the only way to notice is to compare them.
        ///
        /// The neutral Translation.resx is deliberately excluded. New strings are added there first
        /// and reach every culture through ResourceManager's invariant fallback, so a key present only
        /// in neutral is the designed state, not drift.
        /// </summary>
        [Test]
        public void AllLanguageFilesCarryTheSameKeys()
        {
            var dir = Path.Combine(FindRepoRoot(), "SimpleDnsCrypt", "Resources");
            var byLanguage = Directory.GetFiles(dir, "Translation.*.resx")
                .ToDictionary(KeyFromFileName, ReadKeys);

            Assert.Greater(byLanguage.Count, 1, "expected the full set of language files");

            var reference = byLanguage.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).First();
            var baseline = new HashSet<string>(reference.Value);

            var drift = new List<string>();

            foreach (var entry in byLanguage)
            {
                var missing = baseline.Except(entry.Value).ToList();
                var extra = entry.Value.Except(baseline).ToList();

                if (missing.Count > 0 || extra.Count > 0)
                {
                    drift.Add($"{entry.Key}: missing [{string.Join(", ", missing.OrderBy(k => k))}] " +
                              $"unexpected [{string.Join(", ", extra.OrderBy(k => k))}]");
                }
            }

            Assert.IsEmpty(drift,
                $"language files no longer agree with {reference.Key} ({baseline.Count} keys):\n" +
                string.Join("\n", drift));
        }

        private static string KeyFromFileName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.Substring("Translation.".Length).ToLowerInvariant();
        }

        private static HashSet<string> ReadKeys(string resxPath)
        {
            // Parsed, not matched: the resx schema comment contains literal <data name="...">
            // examples, and a regex would read those as keys in every file - which agrees across
            // languages, so a regex-only check would pass while measuring the wrong thing.
            var document = System.Xml.Linq.XDocument.Load(resxPath);

            return document.Root
                .Elements("data")
                .Select(e => (string)e.Attribute("name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet();
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

using System;

namespace SimpleDnsCrypt.Utils
{
    public static class UpdateChecker
    {
        /// <summary>
        /// True when <paramref name="candidateVersion"/> is strictly newer than
        /// <paramref name="current"/>. Anything unparseable is treated as "no update" rather than
        /// guessed, because a wrong answer here either spams the user or, worse, offers a downgrade.
        ///
        /// Requires at least major.minor.patch: "1.0" is rejected rather than read as "1.0.0", so a
        /// malformed manifest cannot quietly become an upgrade offer.
        /// </summary>
        public static bool IsNewer(string candidateVersion, Version current)
        {
            if (current == null || string.IsNullOrWhiteSpace(candidateVersion))
            {
                return false;
            }

            if (!TryParseStrict(candidateVersion, out var candidate))
            {
                return false;
            }

            return candidate > current;
        }

        private static bool TryParseStrict(string text, out Version version)
        {
            version = null;

            var parts = text.Trim().Split('.');

            if (parts.Length < 3 || parts.Length > 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (part.Length == 0 || !int.TryParse(part, out var n) || n < 0)
                {
                    return false;
                }
            }

            // Version.TryParse accepts "1.0" and things like "1.0.0.0.0"; the shape check above has
            // already narrowed the input, so this only has to produce the value.
            return Version.TryParse(text.Trim(), out version);
        }
    }
}

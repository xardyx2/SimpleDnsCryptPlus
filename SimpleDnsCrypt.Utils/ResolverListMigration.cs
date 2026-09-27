using System.Linq;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// Decides whether a resolver-list source entry is safe for the configuration migration to
    /// replace. Kept free of IO and of the TOML model so the rule is testable.
    /// </summary>
    public static class ResolverListMigration
    {
        /// <summary>
        /// True only when the URLs are the untouched upstream v2 default pair, which is the one
        /// case where replacing them with the v3 defaults cannot discard anything the user chose.
        /// Already-v3 lists, custom lists, half-customised lists and lists too short to judge are
        /// all left alone.
        /// </summary>
        public static bool IsOutdatedDefaultList(string[] urls)
        {
            if (urls == null || urls.Length < 2)
            {
                return false;
            }

            return urls.Take(2).All(url => url != null && url.Contains("/v2/"));
        }
    }
}

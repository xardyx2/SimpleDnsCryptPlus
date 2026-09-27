using Nett;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Models;
using System;
using System.IO;

namespace SimpleDnsCrypt.Helper
{
    /// <summary>
    /// Class to load and save the dnscrypt configuration (TOML format).
    /// </summary>
    public static class DnscryptProxyConfigurationManager
    {
        public static TomlSettings ReadTomlSettings =>
            TomlSettings.Create(s => s.ConfigurePropertyMapping(m => m.UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase)));

        /// <summary>
        /// The global dnscrypt configuration.
        /// </summary>
        public static DnscryptProxyConfiguration DnscryptProxyConfiguration { get; set; }

        /// <summary>
        /// Loads the configuration from a .toml file.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static bool LoadConfiguration()
        {
            try
            {
                var configFile = Global.DnsCryptConfigurationFilePath;
                if (!File.Exists(configFile)) return false;
                DnscryptProxyConfiguration = Toml.ReadFile<DnscryptProxyConfiguration>(configFile, ReadTomlSettings);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the configuration to a .toml file.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static bool SaveConfiguration()
        {
            try
            {
                var settings = TomlSettings.Create(s => s.ConfigurePropertyMapping(m => m.UseKeyGenerator(standardGenerators => standardGenerators.LowerCase)));
                Toml.WriteFile(DnscryptProxyConfiguration, Global.DnsCryptConfigurationFilePath, settings);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

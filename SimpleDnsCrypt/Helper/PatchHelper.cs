using System;
using System.Globalization;
using System.IO;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Models;

namespace SimpleDnsCrypt.Helper;

/// <summary>
/// Class to update the configuration file. 
/// </summary>
public static class PatchHelper
{
    private const int ConfigFileVersion = 1;

    public static void Patch()
    {
        if (!File.Exists(Global.DnsCryptConfigurationFilePath))
        {
            File.Copy(
                Global.DnsCryptExampleConfigurationFilePath,
                Global.DnsCryptConfigurationFilePath,
                false);
        }
        else
        {
            var oldVersion = File.Exists(Global.DnsCryptConfigurationVersionFilePath)
                ? int.TryParse(File.ReadAllText(Global.DnsCryptConfigurationVersionFilePath).Trim(), out var parsed)
                    ? parsed
                    : 0
                : 0;
            if (oldVersion == ConfigFileVersion)
            {
                return;
            }

            File.Copy(Global.DnsCryptConfigurationFilePath, $"{Global.DnsCryptConfigurationFilePath}.{DateTime.Now:yyyyMMddTHHmmss}.bak", true);

            if (!DnscryptProxyConfigurationManager.LoadConfiguration())
            {
                return;
            }

            if (oldVersion <= 0)
            {
                DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.sources["public-resolvers"] = new Source
                {
                    urls = new[]
                    {
                        "https://raw.githubusercontent.com/DNSCrypt/dnscrypt-resolvers/master/v3/public-resolvers.md",
                        "https://download.dnscrypt.info/resolvers-list/v3/public-resolvers.md",
                        "https://ipv6.download.dnscrypt.info/resolvers-list/v3/public-resolvers.md",
                    },
                    cache_file = "public-resolvers.md",
                    minisign_key = "RWQf6LRCGA9i53mlYecO4IzT51TGPpvWucNSCh1CBM0QTaLn73Y7GFO3",
                    prefix = "",
                };
                DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.sources["relays"] = new Source
                {
                    urls = new[]
                    {
                        "https://raw.githubusercontent.com/DNSCrypt/dnscrypt-resolvers/master/v3/relays.md",
                        "https://download.dnscrypt.info/resolvers-list/v3/relays.md",
                        "https://ipv6.download.dnscrypt.info/resolvers-list/v3/relays.md",
                    },
                    cache_file = "relays.md",
                    minisign_key = "RWQf6LRCGA9i53mlYecO4IzT51TGPpvWucNSCh1CBM0QTaLn73Y7GFO3",
                    prefix = "",
                    refresh_delay = 72,
                };
            }

            DnscryptProxyConfigurationManager.SaveConfiguration();
        }

        File.WriteAllText(Global.DnsCryptConfigurationVersionFilePath, ConfigFileVersion.ToString(CultureInfo.InvariantCulture));
    }
}
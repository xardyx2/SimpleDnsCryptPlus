using System;
using System.IO;
using System.Reflection;

namespace SimpleDnsCrypt.Config
{
    public static class Global
    {
        /// <summary>
        ///     The name of this application.
        /// </summary>
        public const string ApplicationName = "Simple DNSCrypt";

        /// <summary>
        ///		Output folder for logs.
        /// </summary>
        public const string LogDirectoryName = "logs";

        /// <summary>
        ///     The folder where the dnscrypt-proxy lives in.
        /// </summary>
        public const string DnsCryptProxyFolderName = "dnscrypt-proxy";

        public const string DnsCryptProxyExecutableName86 = "dnscrypt-proxy86.exe";
        public const string DnsCryptProxyExecutableName64 = "dnscrypt-proxy64.exe";

        public const string DnsCryptConfigurationFileName = "dnscrypt-proxy.toml";
        public const string DnsCryptExampleConfigurationFileName = DnsCryptConfigurationFileName + ".example";

        /// <summary>
        ///		Logfile name of dnscrypt-proxy.
        /// </summary>
        public const string DnsCryptLogFileName = "dnscrypt-proxy.log";

        /// <summary>
        ///     Time we wait on a service start (ms).
        /// </summary>
        public const int ServiceStartTime = 2500;

        /// <summary>
        ///     Time we wait on a service stop (ms).
        /// </summary>
        public const int ServiceStopTime = 2500;

        /// <summary>
        ///     Time we wait on a service uninstall (ms).
        /// </summary>
        public const int ServiceUninstallTime = 2500;

        /// <summary>
        ///     Time we wait on a service install (ms).
        /// </summary>
        public const int ServiceInstallTime = 3000;

        public const string DomainBlockLogFileName = "blocked.log";
        public const string QueryLogFileName = "query.log";

        public const string WhitelistRuleFileName = "domain-whitelist.txt";
        public const string BlacklistRuleFileName = "domain-blacklist.txt";
        public const string BlacklistFileName = "blacklist.txt";

        public const string CloakingRulesFileName = "cloaking-rules.txt";
        public const string ForwardingRulesFileName = "forwarding-rules.txt";

        public const string GlobalResolver = "0.0.0.0:53";
        public const string DefaultResolverIpv4 = "127.0.0.1:53";
        public const string DefaultResolverIpv6 = "[::1]:53";

        /// <summary>
        ///     List of the default fall back resolvers.
        /// </summary>
        public static readonly string[] DefaultFallbackResolvers =
        {
            "9.9.9.9:53",
            "8.8.8.8:53"
        };

        /// <summary>
        ///     List of files must exist.
        /// </summary>
        public static readonly string[] DnsCryptProxyFiles =
        {
            DnsCryptProxyExecutableName64,
            DnsCryptProxyExecutableName86,
            DnsCryptExampleConfigurationFileName,
            "LICENSE"
        };

        /// <summary>
        ///     List of interfaces, marked as hidden.
        /// </summary>
        public static readonly string[] NetworkInterfaceBlacklist =
        {
            "Microsoft Virtual",
            "Hamachi Network",
            "VMware Virtual",
            "VirtualBox",
            "Software Loopback",
            "Microsoft ISATAP",
            "Microsoft-ISATAP",
            "Teredo Tunneling Pseudo-Interface",
            "Microsoft Wi-Fi Direct Virtual",
            "Microsoft Teredo Tunneling Adapter",
            "Von Microsoft gehosteter",
            "Microsoft hosted",
            "Virtueller Microsoft-Adapter",
            "TAP"
        };

        public static string InstallPath { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string DnsCryptFolderPath { get; } = Path.Combine(InstallPath, DnsCryptProxyFolderName);

        public static string DnsCryptProxyExecutablePath { get; } = Path.Combine(DnsCryptFolderPath,
            Environment.Is64BitOperatingSystem
                ? DnsCryptProxyExecutableName64
                : DnsCryptProxyExecutableName86);

        public static string DnsCryptConfigurationVersionFilePath { get; } = Path.Combine(DnsCryptFolderPath, "configVersion.txt");
        public static string DnsCryptConfigurationFilePath { get; } = Path.Combine(DnsCryptFolderPath, DnsCryptConfigurationFileName);
        public static string DnsCryptExampleConfigurationFilePath { get; } = Path.Combine(DnsCryptFolderPath, DnsCryptExampleConfigurationFileName);
        public static string DnsCryptLogFilePath { get; } = Path.Combine(InstallPath, LogDirectoryName, DnsCryptLogFileName);
    }
}

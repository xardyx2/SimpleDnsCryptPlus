using Caliburn.Micro;
using Microsoft.Win32;
using Newtonsoft.Json;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Threading.Tasks;
using SimpleDnsCrypt.Extensions;

namespace SimpleDnsCrypt.Helper
{
    public enum ServiceStatus
    {
        NotInstalled,
        Stopped,
        Starting,
        Running
    }

    /// <summary>
    ///     Class to manage the dnscrypt-proxy service and maintain the registry.
    /// </summary>
    public static class DnsCryptProxyManager
    {
        private static readonly ILog Log = LogManagerHelper.Factory();
        private const string DnsCryptProxyServiceName = "dnscrypt-proxy";

        public static IObservable<ServiceStatus> ServiceStatusTracker { get; } = Observable.Interval(TimeSpan.FromSeconds(1)).Select(_ =>
            (TryGetDnsCryptService(out var service), service.Status) switch
            {
                (false, _) => ServiceStatus.NotInstalled,
                (true, ServiceControllerStatus.Running) => ServiceStatus.Running,
                (true, ServiceControllerStatus.StartPending) => ServiceStatus.Starting,
                _ => ServiceStatus.Stopped
            }).Publish().RefCount();

        private static bool TryGetDnsCryptService(out ServiceController service)
        {
            try
            {
                service = new ServiceController { ServiceName = DnsCryptProxyServiceName };
                _ = service.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                service = null;
                return false;
            }
        }

        /// <summary>
        ///     Check if the DNSCrypt proxy service is installed.
        /// </summary>
        /// <returns><c>true</c> if the service is installed, otherwise <c>false</c></returns>
        /// <exception cref="Win32Exception">An error occurred when accessing a system API. </exception>
        public static bool IsDnsCryptProxyInstalled()
        {
            return TryGetDnsCryptService(out _);
        }

        /// <summary>
        ///     Check if the DNSCrypt proxy service is running.
        /// </summary>
        /// <returns><c>true</c> if the service is running, otherwise <c>false</c></returns>
        public static bool IsDnsCryptProxyRunning()
        {
            try
            {
                if (!TryGetDnsCryptService(out var dnscryptService))
                {
                    return false;
                }

                var proxyStatus = dnscryptService.Status;
                switch (proxyStatus)
                {
                    case ServiceControllerStatus.Running:
                        return true;
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.PausePending:
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        return false;
                    default:
                        return false;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return false;
            }
        }

        public static async Task RestartIfRunning()
        {
            if (IsDnsCryptProxyRunning())
            {
                await Restart();
            }
        }

        /// <summary>
        ///     Restart the dnscrypt-proxy service.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        private static async Task<bool> Restart()
        {
            try
            {
                if (!TryGetDnsCryptService(out var dnscryptService))
                {
                    return false;
                }

                await Stop();
                var result = await dnscryptService.StartAsync(TimeSpan.FromMilliseconds(Global.ServiceStartTime));
                await FlushSystemDnsCache();
                return result;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return false;
            }
        }

        /// <summary>
        ///     Stop the dnscrypt-proxy service.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static async Task<bool> Stop()
        {
            try
            {
                if (!TryGetDnsCryptService(out var dnscryptService))
                {
                    return false;
                }

                var proxyStatus = dnscryptService.Status;
                switch (proxyStatus)
                {
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.PausePending:
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.Running:
                        await dnscryptService.StopAsync(TimeSpan.FromMilliseconds(Global.ServiceStopTime));
                        break;
                }
                return dnscryptService.Status == ServiceControllerStatus.Stopped;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return false;
            }
        }

        /// <summary>
        ///     Start the dnscrypt-proxy service.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static async Task<bool> Start()
        {
            try
            {
                if (!TryGetDnsCryptService(out var dnscryptService))
                {
                    return false;
                }

                var proxyStatus = dnscryptService.Status;
                var result = proxyStatus switch
                {
                    ServiceControllerStatus.StartPending => 
                        await dnscryptService.WaitForStatusAsync(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(Global.ServiceStartTime)),
                    ServiceControllerStatus.Running => true,
                    _ => await dnscryptService.StartAsync(TimeSpan.FromMilliseconds(Global.ServiceStartTime))
                };
                await FlushSystemDnsCache();
                return result;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return false;
            }
        }

        public static async Task FlushSystemDnsCache()
        {
            await ProcessHelper.ExecuteWithArgumentsAsync("ipconfig", "/flushdns");
        }

        /// <summary>
        /// Get the version of the dnscrypt-proxy.exe.
        /// </summary>
        /// <returns></returns>
        public static string GetVersion()
        {
            var result = ProcessHelper.ExecuteWithArguments(Global.DnsCryptProxyExecutablePath, "-version");
            return result.Success ? result.StandardOutput.Replace(Environment.NewLine, "") : string.Empty;
        }

        /// <summary>
        ///  Check the configuration file.
        /// </summary>
        /// <returns></returns>
        public static bool IsConfigurationFileValid()
        {
            try
            {
                var result = ProcessHelper.ExecuteWithArguments(Global.DnsCryptProxyExecutablePath, "-check");
                return result.Success;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return false;
            }
        }

        /// <summary>
        /// Get the list of available (active) resolvers for the enabled filters.
        /// </summary>
        /// <returns></returns>
        public static async Task<List<AvailableResolver>> GetAvailableResolvers()
        {
            var resolvers = new List<AvailableResolver>();
            var result = await ProcessHelper.ExecuteWithArgumentsAsync(Global.DnsCryptProxyExecutablePath, "-list -json").ConfigureAwait(false);
            if (!result.Success) return resolvers;
            if (string.IsNullOrEmpty(result.StandardOutput)) return resolvers;
            try
            {
                var res = JsonConvert.DeserializeObject<List<AvailableResolver>>(result.StandardOutput);
                if (res != null)
                {
                    resolvers = res;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            return resolvers;
        }

        /// <summary>
        /// Get the list of all resolvers.
        /// </summary>
        /// <returns></returns>
        public static async Task<List<AvailableResolver>> GetAllResolversWithoutFilters()
        {
            var resolvers = new List<AvailableResolver>();
            var result = await ProcessHelper.ExecuteWithArgumentsAsync(Global.DnsCryptProxyExecutablePath, "-list-all -json").ConfigureAwait(false);
            if (!result.Success) return resolvers;
            if (string.IsNullOrEmpty(result.StandardOutput)) return resolvers;
            try
            {
                var res = JsonConvert.DeserializeObject<List<AvailableResolver>>(result.StandardOutput);
                if (res != null)
                {
                    resolvers = res;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            return resolvers;
        }

        /// <summary>
        /// Install the dnscrypt-proxy service.
        /// </summary>
        /// <returns></returns>
        public static bool Install()
        {
            var result = ProcessHelper.ExecuteWithArguments(Global.DnsCryptProxyExecutablePath, "-service install");
            if (result.Success)
            {
                return true;
            }
            Log.Warn($"Service install failed: {result.StandardError}");
            try
            {
                if (string.IsNullOrEmpty(result.StandardError)) return false;
                if (result.StandardError.Contains("SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\dnscrypt-proxy"))
                {
                    Registry.LocalMachine.DeleteSubKey(@"SYSTEM\CurrentControlSet\Services\EventLog\Application\dnscrypt-proxy",
                        false);
                    return Install();
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }

            return false;
        }

        /// <summary>
        /// Uninstall the dnscrypt-proxy service.
        /// </summary>
        /// <returns></returns>
        public static bool Uninstall()
        {
            var result = ProcessHelper.ExecuteWithArguments(Global.DnsCryptProxyExecutablePath, "-service uninstall");
            try
            {
                var eventLogKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\EventLog\Application\dnscrypt-proxy",
                    RegistryRights.ReadKey);
                var eventLogKeyValue = eventLogKey?.GetValue("CustomSource");
                if (eventLogKeyValue != null)
                {
                    Registry.LocalMachine.DeleteSubKey(@"SYSTEM\CurrentControlSet\Services\EventLog\Application\dnscrypt-proxy", false);
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }

            return result.Success;
        }
    }
}

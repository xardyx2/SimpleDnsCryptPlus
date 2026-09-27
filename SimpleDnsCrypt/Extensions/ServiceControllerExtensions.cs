using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace SimpleDnsCrypt.Extensions
{
    public static class ServiceControllerExtensions
    {
        public static async Task<bool> StartAsync(this ServiceController serviceController, TimeSpan timeout)
        {
            serviceController.Start();
            return await serviceController.WaitForStatusAsync(ServiceControllerStatus.Running, timeout);
        }

        public static async Task<bool> StopAsync(this ServiceController serviceController, TimeSpan timeout)
        {
            serviceController.Stop();
            return await serviceController.WaitForStatusAsync(ServiceControllerStatus.Stopped, timeout);
        }

        public static async Task<bool> WaitForStatusAsync(this ServiceController serviceController, ServiceControllerStatus status, TimeSpan timeout)
        {
            var start = DateTime.UtcNow;
            serviceController.Refresh();
            while (serviceController.Status != status)
            {
                if (DateTime.UtcNow - start > timeout)
                {
                    return false;
                }

                await Task.Delay(250);
                serviceController.Refresh();
            }

            return true;
        }
    }
}

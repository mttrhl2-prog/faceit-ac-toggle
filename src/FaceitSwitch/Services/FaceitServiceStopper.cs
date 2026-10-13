using System.ServiceProcess;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Services;

public sealed class FaceitServiceStopper(ILoggingService log) : IFaceitServiceStopper
{
    public Task<string?> StopAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        try
        {
            using var service = new ServiceController("FACEITService");
            if (service.Status == ServiceControllerStatus.Stopped) return null;
            if (service.Status != ServiceControllerStatus.StopPending) service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            log.Write("FACEITService stopped. Kernel drivers remain loaded until reboot.");
            return (string?)null;
        }
        catch (Exception ex)
        {
            log.Write("Best-effort FACEITService stop failed", ex);
            return $"Could not stop FACEITService: {ex.Message}. Its startup configuration will still be changed; restart is required.";
        }
    }, cancellationToken);
}

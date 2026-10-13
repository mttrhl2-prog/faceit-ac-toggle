using System.Management;
using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Services;

public sealed class RuntimeStateService(ILoggingService log) : IRuntimeStateService
{
    public Task<RuntimeState> ReadAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        bool? integrity = null;
        bool? hypervisor = null;
        var errors = new List<string>();
        try
        {
            using var search = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard",
                "SELECT SecurityServicesRunning FROM Win32_DeviceGuard");
            search.Options.Timeout = TimeSpan.FromSeconds(10);
            using var results = search.Get();
            foreach (ManagementObject item in results)
                using (item)
                    if (item["SecurityServicesRunning"] is Array values)
                        integrity = values.Cast<object>().Any(value => Convert.ToUInt32(value) == 2);
            if (integrity is null) errors.Add("Memory Integrity runtime status unavailable.");
        }
        catch (Exception ex)
        {
            log.Write("Memory Integrity runtime query failed", ex);
            errors.Add($"Memory Integrity runtime query: {ex.Message}");
        }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var search = new ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem");
            search.Options.Timeout = TimeSpan.FromSeconds(10);
            using var results = search.Get();
            foreach (ManagementObject item in results)
                using (item)
                    if (item["HypervisorPresent"] is bool value) hypervisor = value;
            if (hypervisor is null) errors.Add("Hypervisor runtime status unavailable.");
        }
        catch (Exception ex)
        {
            log.Write("Hypervisor runtime query failed", ex);
            errors.Add($"Hypervisor runtime query: {ex.Message}");
        }
        return new RuntimeState(integrity, hypervisor, string.Join(" ", errors));
    }, cancellationToken);
}

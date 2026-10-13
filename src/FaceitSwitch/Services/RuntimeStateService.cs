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
        bool? faceit = null;
        bool? iommu = null;
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
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT Name, State FROM Win32_SystemDriver WHERE Name = 'FACEIT' OR Name = 'FACEIT_IOMMU'");
            search.Options.Timeout = TimeSpan.FromSeconds(10);
            using var results = search.Get();
            // A successful enumeration with no matching driver means it is not running.
            // Commit these results only after the complete enumeration succeeds.
            bool? detectedFaceit = false;
            bool? detectedIommu = false;
            foreach (ManagementObject item in results)
                using (item)
                {
                    var name = item["Name"] as string;
                    var state = item["State"] as string;
                    bool? running = string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) ? true
                        : string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase) ? false : null;
                    if (string.Equals(name, "FACEIT", StringComparison.OrdinalIgnoreCase)) detectedFaceit = running;
                    if (string.Equals(name, "FACEIT_IOMMU", StringComparison.OrdinalIgnoreCase)) detectedIommu = running;
                }
            faceit = detectedFaceit;
            iommu = detectedIommu;
            if (faceit is null) errors.Add("FACEIT driver runtime status unavailable or transitioning.");
            if (iommu is null) errors.Add("FACEIT_IOMMU driver runtime status unavailable or transitioning.");
        }
        catch (Exception ex)
        {
            log.Write("FACEIT driver runtime query failed", ex);
            errors.Add($"FACEIT driver runtime query: {ex.Message}");
        }
        log.Write($"Runtime scan: HVCI={integrity}; hypervisor={hypervisor}; FACEIT={faceit}; FACEIT_IOMMU={iommu}");
        return new RuntimeState(integrity, hypervisor, string.Join(" ", errors), faceit, iommu);
    }, cancellationToken);
}

using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed class FaceitDriverService(IRegistryService registry, IFaceitServiceStopper stopper, ILoggingService log)
    : SettingController(log)
{
    public const string IommuService = @"SYSTEM\CurrentControlSet\Services\FACEIT_IOMMU";
    public const string DriverService = @"SYSTEM\CurrentControlSet\Services\FACEIT";
    public const string UserService = @"SYSTEM\CurrentControlSet\Services\FACEITService";
    public const string Smm = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Smm";
    public override SettingId Id => SettingId.FaceitDrivers;

    protected override Task<SettingReading> ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var iommu = registry.ReadDword(IommuService, "Start");
        var driver = registry.ReadDword(DriverService, "Start");
        var service = registry.ReadDword(UserService, "Start");
        var force = registry.ReadDword(Smm, "ForceEnableIommu");
        var state = iommu.Is(0) && driver.Is(1) && service.Is(3) && force.Is(2)
            ? SettingState.Enabled
            : iommu.Is(4) && driver.Is(4) && service.Is(4) && !force.Exists
                ? SettingState.Disabled : SettingState.Mixed;
        return Task.FromResult(new SettingReading(state,
            $"FACEIT_IOMMU Start={iommu}; FACEIT Start={driver}; FACEITService Start={service}; ForceEnableIommu={force}."));
    }

    protected override async Task ApplyConfigurationAsync(bool enabled, Changes changes, CancellationToken cancellationToken)
    {
        foreach (var path in new[] { IommuService, DriverService, UserService })
            if (!registry.KeyExists(path))
                throw new InvalidOperationException("FACEIT Anti-Cheat is not fully installed. Install or repair it before changing its driver configuration.");

        if (enabled)
        {
            // Pre-create before changing driver starts: FACEIT should need only one reboot.
            changes.Record(registry.WriteDword(Smm, "ForceEnableIommu", 2));
            changes.Record(registry.WriteDword(IommuService, "Start", 0));
            changes.Record(registry.WriteDword(DriverService, "Start", 1));
            changes.Record(registry.WriteDword(UserService, "Start", 3));
        }
        else
        {
            try
            {
                var warning = await stopper.StopAsync(cancellationToken).ConfigureAwait(false);
                if (warning is not null) changes.Warnings.Add(warning);
            }
            catch (Exception ex) { changes.Warnings.Add($"Could not stop FACEITService: {ex.Message}"); }
            changes.Record(registry.WriteDword(IommuService, "Start", 4));
            changes.Record(registry.WriteDword(DriverService, "Start", 4));
            changes.Record(registry.WriteDword(UserService, "Start", 4));
            changes.Record(registry.DeleteValue(Smm, "ForceEnableIommu"));
        }

        // Verify all four values, including DWORD types, before the generic state check.
        if (!registry.ReadDword(IommuService, "Start").Is(enabled ? 0 : 4)
            || !registry.ReadDword(DriverService, "Start").Is(enabled ? 1 : 4)
            || !registry.ReadDword(UserService, "Start").Is(enabled ? 3 : 4)
            || (enabled ? !registry.ReadDword(Smm, "ForceEnableIommu").Is(2)
                : registry.ValueExists(Smm, "ForceEnableIommu")))
            throw new InvalidOperationException("FACEIT driver values did not retain the requested configuration.");
    }
}

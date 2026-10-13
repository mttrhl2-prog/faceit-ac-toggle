using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed class DriverBlocklistService(IRegistryService registry, ILoggingService log) : SettingController(log)
{
    public const string Configuration = @"SYSTEM\CurrentControlSet\Control\CI\Config";
    public override SettingId Id => SettingId.DriverBlocklist;

    protected override Task<SettingReading> ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = registry.ReadDword(Configuration, "VulnerableDriverBlocklistEnable");
        var state = !value.Exists || value.Is(1) ? SettingState.Enabled
            : value.Is(0) ? SettingState.Disabled : SettingState.Unknown;
        return Task.FromResult(new SettingReading(state,
            $"Configured VulnerableDriverBlocklistEnable={value}. Missing uses the Windows 11 default (enabled); effective enforcement can depend on other protections."));
    }

    protected override Task ApplyConfigurationAsync(bool enabled, Changes changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        changes.Record(registry.WriteDword(Configuration, "VulnerableDriverBlocklistEnable", enabled ? 1 : 0));
        if (!registry.ReadDword(Configuration, "VulnerableDriverBlocklistEnable").Is(enabled ? 1 : 0))
            throw new InvalidOperationException("The driver blocklist value did not retain the requested configuration.");
        return Task.CompletedTask;
    }
}

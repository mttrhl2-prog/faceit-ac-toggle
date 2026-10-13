using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed class CoreIsolationService(IRegistryService registry, ILoggingService log) : SettingController(log)
{
    public const string Configuration = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
    public const string Policy = @"SOFTWARE\Policies\Microsoft\Windows\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
    public override SettingId Id => SettingId.MemoryIntegrity;

    protected override Task<SettingReading> ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = registry.ReadDword(Policy, "Enabled");
        var value = registry.ReadDword(Configuration, "Enabled");
        var state = policy.Is(0) ? SettingState.Disabled
            : value.Is(1) ? SettingState.Enabled
            : value.Is(0) || !value.Exists ? SettingState.Disabled : SettingState.Unknown;
        return Task.FromResult(new SettingReading(state, $"Configured Enabled={value}; policy Enabled={policy}. Runtime is reported separately."));
    }

    protected override Task ApplyConfigurationAsync(bool enabled, Changes changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        changes.Record(registry.WriteDword(Configuration, "Enabled", enabled ? 1 : 0));
        changes.Record(registry.WriteDword(Configuration, "WasEnabledBy", enabled ? 2 : 0));
        if (enabled) changes.Record(registry.DeleteValue(Policy, "Enabled"));
        if (!registry.ReadDword(Configuration, "Enabled").Is(enabled ? 1 : 0)
            || !registry.ReadDword(Configuration, "WasEnabledBy").Is(enabled ? 2 : 0)
            || (enabled && registry.ValueExists(Policy, "Enabled")))
            throw new InvalidOperationException("Memory Integrity values did not retain the requested configuration.");
        return Task.CompletedTask;
    }
}

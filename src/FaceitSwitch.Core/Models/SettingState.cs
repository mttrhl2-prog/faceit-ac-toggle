namespace FaceitSwitch.Core.Models;

public enum SettingId { FaceitDrivers, MemoryIntegrity, Hypervisor, DriverBlocklist }
public enum SettingState { Enabled, Disabled, Mixed, Unknown, Error }
public enum ProfileState { Enabled, Disabled, Custom }

public sealed record SettingReading(SettingState State, string Detail);

public sealed record ConfigurationSnapshot(IReadOnlyDictionary<SettingId, SettingReading> Settings)
{
    public ProfileState Profile => Settings.Count == 4 && Settings.Values.All(s => s.State == SettingState.Enabled)
        ? ProfileState.Enabled
        : Settings.Count == 4 && Settings.Values.All(s => s.State == SettingState.Disabled)
            ? ProfileState.Disabled : ProfileState.Custom;
}

public sealed record RuntimeState(bool? MemoryIntegrityRunning, bool? HypervisorPresent, string Detail,
    bool? FaceitDriverRunning = null, bool? FaceitIommuDriverRunning = null)
{
    public bool HasMismatch(ConfigurationSnapshot configuration)
    {
        var faceit = configuration.Settings[SettingId.FaceitDrivers].State;
        return Differs(configuration.Settings[SettingId.MemoryIntegrity].State, MemoryIntegrityRunning)
            || Differs(configuration.Settings[SettingId.Hypervisor].State, HypervisorPresent)
            || (faceit == SettingState.Enabled && (FaceitDriverRunning is false || FaceitIommuDriverRunning is false))
            || (faceit == SettingState.Disabled && (FaceitDriverRunning is true || FaceitIommuDriverRunning is true));
    }

    public bool CanCompare(SettingId id, SettingState configured) => configured is SettingState.Enabled or SettingState.Disabled
        && (id switch
        {
            SettingId.MemoryIntegrity => MemoryIntegrityRunning.HasValue,
            SettingId.Hypervisor => HypervisorPresent.HasValue,
            SettingId.FaceitDrivers => FaceitDriverRunning.HasValue && FaceitIommuDriverRunning.HasValue,
            _ => false // No reliable effective-state API is assumed for the blocklist.
        });

    private static bool Differs(SettingState configured, bool? running) => running.HasValue
        && (configured == SettingState.Enabled && !running.Value || configured == SettingState.Disabled && running.Value);
}

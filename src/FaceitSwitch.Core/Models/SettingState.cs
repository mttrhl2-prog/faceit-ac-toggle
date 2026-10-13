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

public sealed record RuntimeState(bool? MemoryIntegrityRunning, bool? HypervisorPresent, string Detail);

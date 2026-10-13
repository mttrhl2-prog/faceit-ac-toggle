using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Tests;

public sealed class RuntimeMismatchTests
{
    internal static ConfigurationSnapshot Snapshot(SettingState state = SettingState.Enabled)
        => new(Enum.GetValues<SettingId>().ToDictionary(id => id, _ => new SettingReading(state, "test configuration")));

    [Theory]
    [InlineData(SettingId.MemoryIntegrity, SettingState.Enabled, false)]
    [InlineData(SettingId.MemoryIntegrity, SettingState.Disabled, true)]
    [InlineData(SettingId.Hypervisor, SettingState.Enabled, false)]
    [InlineData(SettingId.Hypervisor, SettingState.Disabled, true)]
    public void HvciAndHypervisorMismatchRequiresRestart(SettingId id, SettingState configured, bool running)
    {
        var values = Snapshot().Settings.ToDictionary();
        values[id] = new(configured, "test");
        var runtime = id == SettingId.MemoryIntegrity ? new RuntimeState(running, true, "", true, true)
            : new RuntimeState(true, running, "", true, true);
        Assert.True(runtime.HasMismatch(new(values)));
    }

    [Theory]
    [InlineData(SettingState.Enabled, false, true)]
    [InlineData(SettingState.Enabled, true, false)]
    [InlineData(SettingState.Enabled, false, false)]
    [InlineData(SettingState.Disabled, true, false)]
    [InlineData(SettingState.Disabled, false, true)]
    [InlineData(SettingState.Disabled, true, true)]
    public void FaceitRuntimeChecksBothDrivers(SettingState configured, bool driver, bool iommu)
    {
        var values = Snapshot().Settings.ToDictionary();
        values[SettingId.FaceitDrivers] = new(configured, "test");
        Assert.True(new RuntimeState(true, true, "", driver, iommu).HasMismatch(new(values)));
    }

    [Theory]
    [InlineData(SettingState.Enabled, true)]
    [InlineData(SettingState.Disabled, false)]
    public void MatchingRuntimeDoesNotRequireRestart(SettingState configured, bool running)
        => Assert.False(new RuntimeState(running, running, "", running, running).HasMismatch(Snapshot(configured)));

    [Fact]
    public void UnavailableRuntimeIsNotInventedAsOff()
    {
        var runtime = new RuntimeState(null, null, "WMI unavailable");
        Assert.False(runtime.HasMismatch(Snapshot()));
        foreach (var id in Enum.GetValues<SettingId>()) Assert.False(runtime.CanCompare(id, SettingState.Enabled));
    }

    [Theory]
    [InlineData(SettingState.Enabled, false, null)]
    [InlineData(SettingState.Disabled, null, true)]
    public void KnownDriverMismatchIsVisibleEvenIfOtherDriverIsUnavailable(SettingState configured, bool? driver, bool? iommu)
    {
        var values = Snapshot().Settings.ToDictionary();
        values[SettingId.FaceitDrivers] = new(configured, "test");
        var runtime = new RuntimeState(true, true, "partially unavailable", driver, iommu);
        Assert.True(runtime.HasMismatch(new(values)));
        Assert.False(runtime.CanCompare(SettingId.FaceitDrivers, configured));
    }

    [Fact]
    public void BlocklistHasNoAssumedRuntimeState()
    {
        var values = Snapshot().Settings.ToDictionary();
        values[SettingId.DriverBlocklist] = new(SettingState.Disabled, "test");
        var runtime = new RuntimeState(true, true, "", true, true);
        Assert.False(runtime.HasMismatch(new(values)));
        Assert.False(runtime.CanCompare(SettingId.DriverBlocklist, SettingState.Disabled));
    }

    [Theory]
    [InlineData(SettingState.Error)]
    [InlineData(SettingState.Mixed)]
    [InlineData(SettingState.Unknown)]
    public void IndeterminateConfigurationIsNotMappedToOff(SettingState state)
    {
        var runtime = new RuntimeState(true, true, "", true, true);
        Assert.False(runtime.HasMismatch(Snapshot(state)));
        foreach (var id in Enum.GetValues<SettingId>()) Assert.False(runtime.CanCompare(id, state));
    }

    [Theory]
    [InlineData(SettingState.Enabled, ProfileState.Enabled)]
    [InlineData(SettingState.Disabled, ProfileState.Disabled)]
    [InlineData(SettingState.Mixed, ProfileState.Custom)]
    [InlineData(SettingState.Unknown, ProfileState.Custom)]
    [InlineData(SettingState.Error, ProfileState.Custom)]
    public void ProfileDerivesOnlyFromAllFourConfiguredStates(SettingState state, ProfileState expected)
        => Assert.Equal(expected, Snapshot(state).Profile);

    [Theory]
    [InlineData(SettingState.Disabled)]
    [InlineData(SettingState.Mixed)]
    [InlineData(SettingState.Unknown)]
    [InlineData(SettingState.Error)]
    public void EachExceptionalOrMixedGroupMakesProfileCustom(SettingState state)
    {
        foreach (var id in Enum.GetValues<SettingId>())
        {
            var values = Snapshot().Settings.ToDictionary();
            values[id] = new(state, "test");
            Assert.Equal(ProfileState.Custom, new ConfigurationSnapshot(values).Profile);
        }
    }
}

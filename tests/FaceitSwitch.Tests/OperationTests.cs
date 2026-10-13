using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Tests;

public sealed class OperationTests
{
    [Fact]
    public async Task ProfilesApplyExactValuesAndPreserveUnrelatedValues()
    {
        var f = new Fixture();
        f.Registry.Seed(FaceitDriverService.Smm, "Unrelated", 19);
        f.Registry.Seed(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "IOMMUFlags", 77);
        f.Registry.Seed(CoreIsolationService.Policy, "Enabled", 0);
        f.Registry.Seed(CoreIsolationService.Policy, "Unrelated", 13);
        var enabled = await f.Profiles.ApplyProfileAsync(true);
        Assert.True(enabled.Success);
        Assert.True(enabled.RestartRequired);
        Assert.Equal(ProfileState.Disabled, enabled.Before.Profile);
        Assert.Equal(ProfileState.Enabled, enabled.After.Profile);
        Assert.Equal(("write", FaceitDriverService.Smm, "ForceEnableIommu", (int?)2), f.Registry.Mutations[0]);
        Assert.True(f.Registry.ReadDword(FaceitDriverService.IommuService, "Start").Is(0));
        Assert.True(f.Registry.ReadDword(FaceitDriverService.DriverService, "Start").Is(1));
        Assert.True(f.Registry.ReadDword(FaceitDriverService.UserService, "Start").Is(3));
        Assert.True(f.Registry.ReadDword(CoreIsolationService.Configuration, "WasEnabledBy").Is(2));
        Assert.False(f.Registry.ValueExists(CoreIsolationService.Policy, "Enabled"));
        Assert.Contains(f.Process.Calls, c => c.Executable == "bcdedit.exe"
            && c.Arguments.SequenceEqual(["/set", "{current}", "hypervisorlaunchtype", "auto"]));

        var disabled = await f.Profiles.ApplyProfileAsync(false);
        Assert.True(disabled.Success);
        Assert.Equal(ProfileState.Disabled, disabled.After.Profile);
        Assert.False(f.Registry.ValueExists(FaceitDriverService.Smm, "ForceEnableIommu"));
        Assert.True(f.Registry.KeyExists(FaceitDriverService.Smm));
        Assert.True(f.Registry.ReadDword(FaceitDriverService.Smm, "Unrelated").Is(19));
        Assert.True(f.Registry.ReadDword(CoreIsolationService.Policy, "Unrelated").Is(13));
        Assert.True(f.Registry.ReadDword(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "IOMMUFlags").Is(77));
        Assert.True(f.Registry.ReadDword(CoreIsolationService.Configuration, "WasEnabledBy").Is(0));
        Assert.Equal(1, f.Stopper.Calls);
        Assert.DoesNotContain(f.Registry.Mutations, m => m.Name is "IOMMUFlags" or "Unrelated"
            or "LsaCfgFlags" or "EnableVirtualizationBasedSecurity" or "RequirePlatformSecurityFeatures");
    }

    [Fact]
    public async Task RepeatedProfileIsIdempotentAndDoesNotRequestAnotherRestart()
    {
        var f = new Fixture();
        Assert.True((await f.Profiles.ApplyProfileAsync(true)).Success);
        f.Registry.Mutations.Clear();
        f.Process.Calls.Clear();
        var second = await f.Profiles.ApplyProfileAsync(true);
        Assert.True(second.Success);
        Assert.False(second.RestartRequired);
        Assert.Empty(f.Registry.Mutations);
        Assert.DoesNotContain(f.Process.Calls, c => c.Arguments[0] == "/set");
    }

    [Fact]
    public async Task IndividualToggleLeavesEveryOtherGroupAlone()
    {
        var f = new Fixture();
        var result = await f.Profiles.ApplySettingAsync(SettingId.MemoryIntegrity, true);
        Assert.True(result.Success);
        Assert.Equal(ProfileState.Custom, result.After.Profile);
        Assert.Equal(SettingState.Enabled, result.After.Settings[SettingId.MemoryIntegrity].State);
        Assert.Equal(SettingState.Disabled, result.After.Settings[SettingId.Hypervisor].State);
        Assert.All(f.Registry.Mutations, m => Assert.Equal(CoreIsolationService.Configuration, m.Path));
        Assert.DoesNotContain(f.Process.Calls, c => c.Arguments[0] == "/set");
        Assert.Equal(0, f.Stopper.Calls);
    }

    [Fact]
    public async Task HypervisorToggleChangesOnlyHypervisorLaunchType()
    {
        var f = new Fixture();
        var result = await f.Profiles.ApplySettingAsync(SettingId.Hypervisor, true);
        Assert.True(result.Success);
        Assert.Empty(f.Registry.Mutations);
        var command = Assert.Single(f.Process.Calls, c => c.Arguments[0] == "/set");
        Assert.Equal(new[] { "/set", "{current}", "hypervisorlaunchtype", "auto" }, command.Arguments);
    }

    [Fact]
    public async Task PartialFailureIsReportedAndActualStateRemainsVisible()
    {
        var f = new Fixture();
        f.Registry.DeniedWrite = (DriverBlocklistService.Configuration, "VulnerableDriverBlocklistEnable");
        var result = await f.Profiles.ApplyProfileAsync(true);
        Assert.False(result.Success);
        Assert.True(result.RestartRequired);
        Assert.False(result.Operations[SettingId.DriverBlocklist].Success);
        Assert.Contains("denied", result.Operations[SettingId.DriverBlocklist].Message);
        Assert.Equal(ProfileState.Custom, result.After.Profile);
        Assert.Equal(SettingState.Disabled, result.After.Settings[SettingId.DriverBlocklist].State);
        Assert.Equal(SettingState.Enabled, result.After.Settings[SettingId.FaceitDrivers].State);
        Assert.Same(result.Before, f.Profiles.LastSnapshotBeforeOperation);
        Assert.Equal(ProfileState.Disabled, result.Before.Profile); // Snapshot is not overwritten by writes.
    }

    [Fact]
    public async Task PartialDriverFailureStillFlagsPendingRestart()
    {
        var f = new Fixture();
        f.Registry.DeniedWrite = (FaceitDriverService.DriverService, "Start");
        var result = await f.Profiles.ApplySettingAsync(SettingId.FaceitDrivers, true);
        Assert.False(result.Success);
        Assert.True(result.RestartRequired);
        Assert.Equal(SettingState.Mixed, result.After.Settings[SettingId.FaceitDrivers].State);
        Assert.True(f.Registry.ReadDword(FaceitDriverService.Smm, "ForceEnableIommu").Is(2));
    }

    [Fact]
    public async Task WriteReadbackFailureCannotReportSuccess()
    {
        var f = new Fixture();
        f.Registry.IgnoreWrite = (CoreIsolationService.Configuration, "WasEnabledBy");
        var result = await f.Profiles.ApplySettingAsync(SettingId.MemoryIntegrity, true);
        Assert.False(result.Success);
        Assert.True(result.RestartRequired);
        Assert.Contains("did not retain", result.Operations[SettingId.MemoryIntegrity].Message);
    }

    [Fact]
    public async Task MissingFaceitInstallationDoesNotCreatePhantomServices()
    {
        var registry = new FakeRegistry();
        var controller = new FaceitDriverService(registry, new FakeStopper(), new FakeLog());
        var result = await controller.EnableAsync();
        Assert.False(result.Success);
        Assert.Contains("not fully installed", result.Message);
        Assert.Empty(registry.Mutations);
        Assert.Empty(registry.Keys);
    }

    [Fact]
    public async Task FaceitStopWarningDoesNotPreventDisablingDriverStartup()
    {
        var f = new Fixture();
        await f.Profiles.ApplySettingAsync(SettingId.FaceitDrivers, true);
        f.Stopper.Warning = "Service stop timed out";
        var result = await f.Profiles.ApplySettingAsync(SettingId.FaceitDrivers, false);
        Assert.True(result.Success);
        Assert.Equal(SettingState.Disabled, result.After.Settings[SettingId.FaceitDrivers].State);
        Assert.Contains("timed out", Assert.Single(result.Operations[SettingId.FaceitDrivers].Warnings!));
    }

    [Fact]
    public async Task BcdExitCodeAndBothStreamsAreIncludedInFailure()
    {
        var f = new Fixture();
        f.Process.FailWrite = true;
        var result = await f.Profiles.ApplySettingAsync(SettingId.Hypervisor, true);
        Assert.False(result.Success);
        var message = result.Operations[SettingId.Hypervisor].Message;
        Assert.Contains("exit 9", message);
        Assert.Contains("operation failed", message);
        Assert.Contains("BCD write denied", message);
        Assert.Equal(SettingState.Disabled, result.After.Settings[SettingId.Hypervisor].State);
    }

    [Fact]
    public async Task BcdReadFailureIsNotMistakenForAbsentDefault()
    {
        var f = new Fixture();
        f.Process.FailRead = true;
        var scan = await f.Profiles.ReadAsync();
        Assert.Equal(SettingState.Error, scan.Settings[SettingId.Hypervisor].State);
        Assert.Contains("exit 5", scan.Settings[SettingId.Hypervisor].Detail);
    }

    [Fact]
    public async Task BcdSuccessWithoutRequestedReadbackFailsVerification()
    {
        var f = new Fixture();
        f.Process.IgnoreWrite = true;
        var result = await f.Profiles.ApplySettingAsync(SettingId.Hypervisor, true);
        Assert.False(result.Success);
        Assert.Contains("Verification failed", result.Operations[SettingId.Hypervisor].Message);
    }
}

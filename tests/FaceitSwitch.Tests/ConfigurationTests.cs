using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task StartupScanIsReadOnlyAndUsesActualValues()
    {
        var f = new Fixture();
        f.Registry.Seed(FaceitDriverService.IommuService, "Start", 0); // Deliberately mixed.
        f.Registry.Seed(CoreIsolationService.Configuration, "Enabled", 1);
        var scan = await f.Profiles.ReadAsync();
        Assert.Equal(SettingState.Mixed, scan.Settings[SettingId.FaceitDrivers].State);
        Assert.Equal(SettingState.Enabled, scan.Settings[SettingId.MemoryIntegrity].State);
        Assert.Equal(ProfileState.Custom, scan.Profile);
        Assert.Empty(f.Registry.Mutations);
        Assert.All(f.Process.Calls, call => Assert.Equal(new[] { "/enum", "{current}" }, call.Arguments));
        Assert.Equal(0, f.Stopper.Calls);
    }

    [Theory]
    [InlineData(0, 1, 3, 2, SettingState.Enabled)]
    [InlineData(4, 4, 4, null, SettingState.Disabled)]
    [InlineData(0, 1, 3, null, SettingState.Mixed)]
    [InlineData(0, 1, 3, 0, SettingState.Mixed)]
    [InlineData(4, 4, 4, 0, SettingState.Mixed)]
    [InlineData(4, 4, 4, 2, SettingState.Mixed)]
    [InlineData(0, 4, 3, 2, SettingState.Mixed)]
    [InlineData(null, null, null, null, SettingState.Mixed)]
    public async Task FaceitDetectionRequiresAllFourExactValues(int? iommu, int? driver, int? service, int? force, SettingState expected)
    {
        var registry = new FakeRegistry();
        if (iommu is { } i) registry.Seed(FaceitDriverService.IommuService, "Start", i);
        if (driver is { } d) registry.Seed(FaceitDriverService.DriverService, "Start", d);
        if (service is { } s) registry.Seed(FaceitDriverService.UserService, "Start", s);
        if (force is { } v) registry.Seed(FaceitDriverService.Smm, "ForceEnableIommu", v);
        var controller = new FaceitDriverService(registry, new FakeStopper(), new FakeLog());
        Assert.Equal(expected, (await controller.ReadStateAsync()).State);
        Assert.Empty(registry.Mutations);
    }

    [Theory]
    [InlineData(0, 1, SettingState.Disabled)]
    [InlineData(0, null, SettingState.Disabled)]
    [InlineData(null, 1, SettingState.Enabled)]
    [InlineData(1, 1, SettingState.Enabled)]
    [InlineData(1, 0, SettingState.Disabled)]
    [InlineData(null, 0, SettingState.Disabled)]
    [InlineData(null, null, SettingState.Disabled)]
    [InlineData(null, 7, SettingState.Unknown)]
    [InlineData(7, 1, SettingState.Enabled)]
    public async Task MemoryIntegrityRespectsDisabledPolicyAndDefaults(int? policy, int? value, SettingState expected)
    {
        var registry = new FakeRegistry();
        if (policy is { } p) registry.Seed(CoreIsolationService.Policy, "Enabled", p);
        if (value is { } v) registry.Seed(CoreIsolationService.Configuration, "Enabled", v);
        var service = new CoreIsolationService(registry, new FakeLog());
        Assert.Equal(expected, (await service.ReadStateAsync()).State);
        Assert.Empty(registry.Mutations);
    }

    [Theory]
    [InlineData(null, SettingState.Enabled)]
    [InlineData(1, SettingState.Enabled)]
    [InlineData(0, SettingState.Disabled)]
    [InlineData(2, SettingState.Unknown)]
    public async Task BlocklistUsesModernWindowsDefaultAndReportsUnexpectedValues(int? value, SettingState expected)
    {
        var registry = new FakeRegistry();
        if (value is { } v) registry.Seed(DriverBlocklistService.Configuration, "VulnerableDriverBlocklistEnable", v);
        var service = new DriverBlocklistService(registry, new FakeLog());
        Assert.Equal(expected, (await service.ReadStateAsync()).State);
        Assert.Empty(registry.Mutations);
    }

    [Fact]
    public async Task WrongRegistryTypeIsNotMistakenForEnabledDword()
    {
        var registry = new FakeRegistry();
        registry.Values[(DriverBlocklistService.Configuration, "VulnerableDriverBlocklistEnable")] = new(true, false, 1);
        Assert.Equal(SettingState.Unknown, (await new DriverBlocklistService(registry, new FakeLog()).ReadStateAsync()).State);
    }

    [Theory]
    [InlineData("Boot loader\r\nhypervisorlaunchtype    Auto\r\n", SettingState.Enabled)]
    [InlineData("Boot loader\nhypervisorlaunchtype off\n", SettingState.Disabled)]
    [InlineData("Boot loader\nidentifier {current}\n", SettingState.Enabled)]
    [InlineData("Boot loader\nhypervisorlaunchtype unexpected\n", SettingState.Unknown)]
    [InlineData("hypervisorlaunchtype Auto\nhypervisorlaunchtype Off\n", SettingState.Unknown)]
    [InlineData("", SettingState.Error)]
    public void HypervisorParsesCurrentBootEntry(string output, SettingState expected)
        => Assert.Equal(expected, HypervisorService.ParseConfiguration(output).State);

    [Fact]
    public async Task UnreadableRegistryShowsErrorWithoutMutations()
    {
        var f = new Fixture();
        f.Registry.ThrowOnRead = true;
        var scan = await f.Profiles.ReadAsync();
        Assert.Equal(SettingState.Error, scan.Settings[SettingId.FaceitDrivers].State);
        Assert.Contains("denied", scan.Settings[SettingId.FaceitDrivers].Detail);
        Assert.Equal(ProfileState.Custom, scan.Profile);
        Assert.Empty(f.Registry.Mutations);
    }
}

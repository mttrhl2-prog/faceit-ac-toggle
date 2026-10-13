using System.Globalization;
using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;
using FaceitSwitch.Services;
using FaceitSwitch.Tests;
using FaceitSwitch.ViewModels;

namespace FaceitSwitch.Ui.Tests;

public sealed class ViewModelTests
{
    private sealed class FakeRuntime : IRuntimeStateService
    {
        public RuntimeState State { get; set; } = new(false, false, "", false, false);
        public Task<RuntimeState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
    }

    private static MainViewModel Create(Fixture fixture, FakeRuntime runtime) => new(fixture.Profiles, runtime,
        new RestartService(fixture.Process, fixture.Log), fixture.Log, () => false);

    [Theory]
    [InlineData(SettingState.Enabled, "", "disable")]
    [InlineData(SettingState.Disabled, "", "enable")]
    [InlineData(SettingState.Mixed, "Mixed", "enable")]
    [InlineData(SettingState.Unknown, "Unknown", "enable")]
    [InlineData(SettingState.Error, "Error", "enable")]
    public void VisibleLabelsHideNormalStatesWithoutLosingAccessibility(SettingState state, string label, string action)
    {
        var owner = Create(new Fixture(), new FakeRuntime());
        var setting = owner.Settings.Single(s => s.Id == SettingId.Hypervisor);
        setting.Update(new(state, "Exact BCD failure detail"));
        Assert.Equal(label, setting.StateLabel);
        Assert.Equal(label.Length > 0, setting.HasStateLabel);
        Assert.Contains(setting.Name, setting.AccessibleLabel);
        Assert.Contains($"configured state {state}", setting.AccessibleLabel);
        Assert.Contains($"Click to {action}", setting.AccessibleLabel);
        Assert.Equal("Exact BCD failure detail", setting.Detail);
        if (state == SettingState.Error)
        {
            Assert.True(setting.IsError);
            Assert.True(setting.IsIndeterminate);
            Assert.False(setting.IsConfiguredEnabled);
            Assert.Contains(setting.Detail, setting.AccessibleLabel);
        }
    }

    [Fact]
    public async Task StartupAndRefreshRemainReadOnlyAndRetryScanErrors()
    {
        var f = new Fixture();
        f.Process.FailRead = true;
        var vm = Create(f, new FakeRuntime());
        await vm.InitializeAsync();
        Assert.Equal("Error", vm.Settings.Single(s => s.Id == SettingId.Hypervisor).StateLabel);
        Assert.Contains("BCD access denied", vm.StatusMessage);
        Assert.True(vm.IsError);
        Assert.Equal("Custom", vm.CurrentState);
        Assert.False(vm.IsEnabledProfileActive);
        Assert.False(vm.IsDisabledProfileActive);
        f.Process.FailRead = false;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Disabled", vm.CurrentState);
        Assert.False(vm.IsError);
        Assert.Empty(f.Registry.Mutations);
        Assert.All(f.Process.Calls, c => Assert.Equal("/enum", c.Arguments[0]));
        Assert.Equal(0, f.Stopper.Calls);
    }

    [Fact]
    public async Task ProfileFlagsFollowActualConfiguredStateAndIndividualChanges()
    {
        var f = new Fixture();
        var runtime = new FakeRuntime();
        var vm = Create(f, runtime);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        await vm.InitializeAsync();
        Assert.True(vm.IsDisabledProfileActive);
        Assert.False(vm.IsEnabledProfileActive);
        Assert.False(vm.IsCustomProfile);
        await vm.EnableProfileCommand.ExecuteAsync(null);
        Assert.Equal("Enabled", vm.CurrentState);
        Assert.True(vm.IsEnabledProfileActive);
        Assert.False(vm.IsDisabledProfileActive);
        Assert.True(vm.RestartRequired);
        await vm.Settings.Single(s => s.Id == SettingId.Hypervisor).ToggleCommand.ExecuteAsync(null);
        Assert.Equal("Custom", vm.CurrentState);
        Assert.True(vm.IsCustomProfile);
        Assert.False(vm.IsEnabledProfileActive);
        Assert.False(vm.IsDisabledProfileActive);
        Assert.Equal(SettingState.Enabled, (await f.Profiles.ReadAsync()).Settings[SettingId.MemoryIntegrity].State);
        Assert.Contains(nameof(MainViewModel.IsEnabledProfileActive), changed);
        Assert.Contains(nameof(MainViewModel.IsDisabledProfileActive), changed);
        Assert.Contains(nameof(MainViewModel.RestartRequired), changed);
    }

    [Fact]
    public async Task ReopeningReconstructsDriverRestartRequirementWithoutCachedState()
    {
        var f = new Fixture(); // All four settings disabled.
        var runtime = new FakeRuntime { State = new(false, false, "", true, true) };
        var first = Create(f, runtime);
        await first.InitializeAsync();
        Assert.Equal("Disabled", first.CurrentState);
        Assert.True(first.RestartRequired);
        Assert.Contains("Restart required", first.FooterText);
        var reopened = Create(f, runtime);
        await reopened.InitializeAsync();
        Assert.True(reopened.RestartRequired);
        Assert.False(reopened.Settings.Single(s => s.Id == SettingId.FaceitDrivers).IsConfiguredEnabled);
        Assert.Empty(f.Registry.Mutations);
        runtime.State = new(false, false, "", false, false); // Evidence observed after reboot.
        await reopened.RefreshCommand.ExecuteAsync(null);
        Assert.False(reopened.RestartRequired);
        Assert.Equal("Changes require a restart to fully apply.", reopened.FooterText);
    }

    [Fact]
    public async Task MatchingRuntimeReplacesWriteFallbackInsteadOfLeavingStickyBoolean()
    {
        var f = new Fixture();
        var runtime = new FakeRuntime();
        var vm = Create(f, runtime);
        await vm.InitializeAsync();
        await vm.ToggleAsync(SettingId.Hypervisor);
        Assert.True(vm.RestartRequired);
        runtime.State = new(false, true, "", false, false);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.RestartRequired);
        Assert.False(vm.RuntimeMismatch);
    }

    [Fact]
    public async Task UnavailableRuntimeRemainsVisibleAndRetainsChangedSettingFallback()
    {
        var f = new Fixture();
        var runtime = new FakeRuntime { State = new(null, null, "WMI query denied") };
        var vm = Create(f, runtime);
        await vm.InitializeAsync();
        Assert.Contains("WMI query denied", vm.StatusMessage);
        Assert.Contains("unavailable", vm.RuntimeSummary);
        Assert.False(vm.RuntimeMismatch);
        await vm.ToggleAsync(SettingId.Hypervisor);
        Assert.True(vm.RestartRequired);
    }

    [Fact]
    public async Task BlocklistWriteUsesSessionFallbackWithoutInventingEffectiveState()
    {
        var vm = Create(new Fixture(), new FakeRuntime());
        await vm.InitializeAsync();
        Assert.False(vm.RestartRequired);
        await vm.ToggleAsync(SettingId.DriverBlocklist);
        Assert.False(vm.RuntimeMismatch);
        Assert.True(vm.RestartRequired);
    }

    [Fact]
    public async Task CancelledRestartConfirmationNeverRunsShutdown()
    {
        var f = new Fixture();
        var vm = Create(f, new FakeRuntime());
        await vm.InitializeAsync();
        await vm.RestartCommand.ExecuteAsync(null);
        Assert.DoesNotContain(f.Process.Calls, c => c.Executable == "shutdown.exe");
    }

    [Theory]
    [InlineData("cmd.exe", "/c", "echo")]
    [InlineData("powershell.exe", "-Command", "Get-Item")]
    [InlineData("reg.exe", "query", "HKLM")]
    [InlineData("bcdedit.exe", "/set", "hypervisoriommupolicy")]
    [InlineData("bcdedit.exe", "/deletevalue", "hypervisorlaunchtype")]
    [InlineData("shutdown.exe", "/r", "/f")]
    public async Task NativeProcessAdapterRejectsUndocumentedCommandsBeforeStartingAnything(string executable, string first, string second)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessService(new FakeLog()).RunAsync(executable, [first, second]));

    [Fact]
    public async Task NativeBcdReadCapturesOutputEvenUnderUnsupportedCultureOemMapping()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-FI");
            var log = new FakeLog();
            var result = await new ProcessService(log).RunAsync("bcdedit.exe", ["/enum", "{current}"]);
            if (result.ExitCode == 0)
            {
                var reading = HypervisorService.ParseConfiguration(result.StandardOutput);
                Assert.True(reading.State is SettingState.Enabled or SettingState.Disabled, reading.Detail);
            }
            else
            {
                // A non-elevated runner may be denied BCD reads. Execution/decoding must still work,
                // with an actual exit code and diagnostic output rather than an encoding exception.
                Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput + result.StandardError));
            }
            Assert.Contains(log.Entries, e => e.Contains("bcdedit.exe; exit=", StringComparison.Ordinal));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }
}

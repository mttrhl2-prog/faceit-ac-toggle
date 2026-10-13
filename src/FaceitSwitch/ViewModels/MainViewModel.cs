using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;
using FaceitSwitch.Services;

namespace FaceitSwitch.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProfileService profiles;
    private readonly IRuntimeStateService runtimeService;
    private readonly RestartService restart;
    private readonly LoggingService log;
    private readonly Func<bool> confirmRestart;
    private ConfigurationSnapshot? snapshot;
    private RuntimeState? runtime;
    private bool isBusy;
    private bool restartRequired;
    private string statusMessage = "Reading the actual Windows configuration…";
    private bool isError;

    public ObservableCollection<SettingViewModel> Settings { get; } = [];
    public IAsyncRelayCommand EnableProfileCommand { get; }
    public IAsyncRelayCommand DisableProfileCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }
    public bool IsBusy => isBusy;
    public bool CanModify => !isBusy && snapshot is not null;
    public string CurrentState => snapshot?.Profile.ToString() ?? "Scanning…";
    public bool RestartRequired => restartRequired;
    public string StatusMessage => statusMessage;
    public bool HasStatus => !string.IsNullOrWhiteSpace(statusMessage);
    public bool IsError => isError;
    public string DependencyWarning => snapshot is not null
        && snapshot.Settings[SettingId.MemoryIntegrity].State == SettingState.Enabled
        && snapshot.Settings[SettingId.Hypervisor].State == SettingState.Disabled
            ? "Memory Integrity is configured but cannot run while the Windows hypervisor is disabled." : "";
    public bool HasDependencyWarning => DependencyWarning.Length > 0;
    public string RuntimeSummary => runtime is null ? "Runtime: scanning…"
        : $"Runtime: Memory Integrity {RuntimeLabel(runtime.MemoryIntegrityRunning)} · Hypervisor {RuntimeLabel(runtime.HypervisorPresent)}";
    public string RuntimeDetail => runtime?.Detail ?? "Runtime is separate from configured state.";
    public bool RuntimeMismatch => snapshot is not null && runtime is not null
        && (Differs(snapshot.Settings[SettingId.MemoryIntegrity].State, runtime.MemoryIntegrityRunning)
            || Differs(snapshot.Settings[SettingId.Hypervisor].State, runtime.HypervisorPresent));
    public string FooterText => RestartRequired ? "Restart required — configured changes are pending."
        : RuntimeMismatch ? "Configured and runtime states differ. A restart may be required."
        : "Changes require a restart to fully apply.";

    public MainViewModel(ProfileService profiles, IRuntimeStateService runtimeService, RestartService restart,
        LoggingService log, Func<bool> confirmRestart)
    {
        this.profiles = profiles; this.runtimeService = runtimeService; this.restart = restart;
        this.log = log; this.confirmRestart = confirmRestart;
        EnableProfileCommand = new AsyncRelayCommand(() => ApplyAsync(null, true), () => CanModify);
        DisableProfileCommand = new AsyncRelayCommand(() => ApplyAsync(null, false), () => CanModify);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        RestartCommand = new AsyncRelayCommand(RestartAsync, () => CanModify);
        Settings.Add(new(SettingId.FaceitDrivers, "FACEIT Drivers",
            "Controls the FACEIT driver configuration used for FACEIT Anti-Cheat.", "\uE83D", this));
        Settings.Add(new(SettingId.MemoryIntegrity, "Core Isolation / Memory Integrity",
            "Hypervisor-enforced code integrity for Windows kernel protection.", "\uE950", this));
        Settings.Add(new(SettingId.Hypervisor, "Virtualization-Based Security (VBS)",
            "Controls whether the Windows hypervisor launches at startup.", "\uE7F4", this));
        Settings.Add(new(SettingId.DriverBlocklist, "Vulnerable Driver Blocklist",
            "Microsoft-maintained blocklist of known malicious or vulnerable kernel drivers.", "\uE7BA", this));
    }

    public Task InitializeAsync() => RefreshAsync();
    public Task ToggleAsync(SettingId id) => ApplyAsync(id, snapshot?.Settings[id].State != SettingState.Enabled);

    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        SetBusy(true);
        SetStatus("Reading the actual Windows configuration…", false);
        try
        {
            var configuration = Task.Run(() => profiles.ReadAsync());
            var effective = runtimeService.ReadAsync();
            await Task.WhenAll(configuration, effective);
            UpdateSnapshot(await configuration);
            runtime = await effective;
            NotifyRuntime();
            var failures = snapshot!.Settings.Where(s => s.Value.State == SettingState.Error)
                .Select(s => $"{s.Key}: {s.Value.Detail}").ToArray();
            SetStatus(failures.Length > 0 ? string.Join(Environment.NewLine, failures) : log.LastError ?? "", failures.Length > 0);
        }
        catch (Exception ex) { log.Write("Configuration scan failed", ex); SetStatus(ex.Message, true); }
        finally { SetBusy(false); }
    }

    private async Task ApplyAsync(SettingId? id, bool enabled)
    {
        if (!CanModify) return;
        SetBusy(true);
        SetStatus("Applying and verifying the requested configuration…", false);
        try
        {
            var result = await Task.Run(() => id is { } setting
                ? profiles.ApplySettingAsync(setting, enabled) : profiles.ApplyProfileAsync(enabled));
            if (result.RestartRequired)
            {
                restartRequired = true;
                OnPropertyChanged(nameof(RestartRequired));
            }
            UpdateSnapshot(result.After);
            // Query again; never pretend that a configuration write changes runtime immediately.
            runtime = await runtimeService.ReadAsync();
            NotifyRuntime();
            var errors = result.Operations.Values.Where(o => !o.Success).Select(o => o.Message);
            var warnings = result.Operations.Values.SelectMany(o => o.Warnings ?? []);
            var messages = errors.Concat(warnings).ToList();
            if (log.LastError is { } loggingError) messages.Add(loggingError);
            SetStatus(messages.Count > 0 ? string.Join(Environment.NewLine, messages)
                : result.RestartRequired ? "Configuration verified. Restart Windows to apply the changes."
                : "The requested configuration is already set.", !result.Success);
        }
        catch (Exception ex)
        {
            log.Write("Configuration operation failed", ex);
            SetStatus(ex.Message, true);
            // A fresh scan makes partial changes visible even after unexpected failures.
            try { UpdateSnapshot(await Task.Run(() => profiles.ReadAsync())); }
            catch (Exception scanError) { log.Write("Recovery scan failed", scanError); SetStatus($"{ex.Message}\nRescan failed: {scanError.Message}", true); }
        }
        finally { SetBusy(false); }
    }

    private async Task RestartAsync()
    {
        if (!CanModify || !confirmRestart()) return;
        SetBusy(true);
        try
        {
            var result = await restart.RestartAsync();
            SetStatus(result.Message, !result.Success);
        }
        catch (Exception ex) { log.Write("Restart request failed", ex); SetStatus(ex.Message, true); }
        finally { SetBusy(false); }
    }

    private void UpdateSnapshot(ConfigurationSnapshot value)
    {
        snapshot = value;
        foreach (var setting in Settings) setting.Update(value.Settings[setting.Id]);
        foreach (var property in new[] { nameof(CurrentState), nameof(DependencyWarning), nameof(HasDependencyWarning),
                     nameof(RuntimeMismatch), nameof(FooterText) }) OnPropertyChanged(property);
    }

    private void NotifyRuntime()
    {
        foreach (var property in new[] { nameof(RuntimeSummary), nameof(RuntimeDetail), nameof(RuntimeMismatch), nameof(FooterText) })
            OnPropertyChanged(property);
    }

    private void SetBusy(bool value)
    {
        isBusy = value;
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanModify));
        foreach (var command in new[] { EnableProfileCommand, DisableProfileCommand, RefreshCommand, RestartCommand }) command.NotifyCanExecuteChanged();
        foreach (var setting in Settings) setting.ToggleCommand.NotifyCanExecuteChanged();
    }

    private void SetStatus(string value, bool error)
    {
        statusMessage = value; isError = error;
        OnPropertyChanged(nameof(StatusMessage)); OnPropertyChanged(nameof(HasStatus)); OnPropertyChanged(nameof(IsError));
    }

    private static string RuntimeLabel(bool? value) => value is true ? "running" : value is false ? "not running" : "unavailable";
    private static bool Differs(SettingState configured, bool? effective) => effective is { } running
        && (configured == SettingState.Enabled && !running || configured == SettingState.Disabled && running);
}

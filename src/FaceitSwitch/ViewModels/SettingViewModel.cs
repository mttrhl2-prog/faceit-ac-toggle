using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceitSwitch.Core.Models;

namespace FaceitSwitch.ViewModels;

public sealed class SettingViewModel : ObservableObject
{
    private SettingReading reading = new(SettingState.Unknown, "Waiting for the startup scan.");
    private bool scanned;
    public SettingId Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Icon { get; }
    public IAsyncRelayCommand ToggleCommand { get; }
    public bool IsConfiguredEnabled => reading.State == SettingState.Enabled;
    public bool IsIndeterminate => reading.State is SettingState.Mixed or SettingState.Unknown or SettingState.Error;
    public bool IsError => reading.State == SettingState.Error;
    public string ToggleGlyph => reading.State switch
    {
        SettingState.Enabled => "✓", SettingState.Disabled => "×", SettingState.Mixed => "–",
        SettingState.Error => "!", _ => "?"
    };
    public string StateLabel => !scanned ? "Scanning…" : reading.State switch
    {
        SettingState.Enabled or SettingState.Disabled => "",
        _ => reading.State.ToString()
    };
    public bool HasStateLabel => StateLabel.Length > 0;
    public string Detail => reading.Detail;
    public string AccessibleLabel => $"{Name}, configured state {(scanned ? reading.State.ToString() : "scanning")}. "
        + $"Click to {(IsConfiguredEnabled ? "disable" : "enable")}."
        + (IsError ? $" Error: {reading.Detail}" : "");

    public SettingViewModel(SettingId id, string name, string description, string icon, MainViewModel owner)
    {
        Id = id; Name = name; Description = description; Icon = icon;
        ToggleCommand = new AsyncRelayCommand(() => owner.ToggleAsync(id), () => owner.CanModify);
    }

    public void Update(SettingReading value)
    {
        reading = value;
        scanned = true;
        foreach (var name in new[] { nameof(IsConfiguredEnabled), nameof(IsIndeterminate), nameof(ToggleGlyph),
                     nameof(IsError), nameof(StateLabel), nameof(HasStateLabel), nameof(Detail), nameof(AccessibleLabel) }) OnPropertyChanged(name);
    }
}

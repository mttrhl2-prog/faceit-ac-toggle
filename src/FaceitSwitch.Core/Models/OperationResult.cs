namespace FaceitSwitch.Core.Models;

public sealed record OperationResult(bool Success, string Message, bool RestartRequired = false,
    IReadOnlyList<string>? Warnings = null);

public sealed record ProfileOperationResult(ConfigurationSnapshot Before, ConfigurationSnapshot After,
    IReadOnlyDictionary<SettingId, OperationResult> Operations)
{
    public bool Success => Operations.Values.All(o => o.Success);
    public bool RestartRequired => Operations.Values.Any(o => o.RestartRequired);
}

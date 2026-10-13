using System.Text.RegularExpressions;
using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed partial class HypervisorService(IProcessService process, ILoggingService log) : SettingController(log)
{
    public override SettingId Id => SettingId.Hypervisor;

    protected override async Task<SettingReading> ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        var result = await process.RunAsync("bcdedit.exe", ["/enum", "{current}"], cancellationToken).ConfigureAwait(false);
        RequireSuccess(result);
        return ParseConfiguration(result.StandardOutput);
    }

    public static SettingReading ParseConfiguration(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return new(SettingState.Error, "BCDEdit returned no boot entry to inspect.");
        var matches = LaunchTypeRegex().Matches(output);
        if (matches.Count == 0)
            return new(SettingState.Enabled, "hypervisorlaunchtype is absent: Windows default is Auto. Runtime is reported separately.");
        if (matches.Count != 1)
            return new(SettingState.Unknown, "BCDEdit returned multiple hypervisorlaunchtype entries.");
        var value = matches[0].Groups[1].Value;
        var state = value.Equals("auto", StringComparison.OrdinalIgnoreCase) ? SettingState.Enabled
            : value.Equals("off", StringComparison.OrdinalIgnoreCase) ? SettingState.Disabled : SettingState.Unknown;
        return new(state, $"Configured hypervisorlaunchtype={value}. This controls hypervisor launch only, not the full VBS configuration.");
    }

    protected override async Task ApplyConfigurationAsync(bool enabled, Changes changes, CancellationToken cancellationToken)
    {
        var before = await ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (before.State == (enabled ? SettingState.Enabled : SettingState.Disabled)) return;
        // Target the same boot entry that was scanned. No other BCD elements are changed.
        ProcessResult result;
        try
        {
            result = await process.RunAsync("bcdedit.exe",
                ["/set", "{current}", "hypervisorlaunchtype", enabled ? "auto" : "off"], cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A timed-out writer may have committed its change before being interrupted.
            changes.Record(true);
            throw;
        }
        RequireSuccess(result);
        changes.Record(true);
    }

    private static void RequireSuccess(ProcessResult result)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"BCDEdit failed (exit {result.ExitCode}). {result.StandardOutput.Trim()} {result.StandardError.Trim()}");
    }

    [GeneratedRegex(@"^\s*hypervisorlaunchtype\s+(\S+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LaunchTypeRegex();
}

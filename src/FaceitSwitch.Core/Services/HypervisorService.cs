using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed class HypervisorService(IProcessService process, ILoggingService log) : SettingController(log)
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
        output = output.TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(output))
            return new(SettingState.Error, "BCDEdit returned no boot entry to inspect.");
        var values = new List<string>();
        using var lines = new StringReader(output);
        while (lines.ReadLine() is { } line)
        {
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("hypervisorlaunchtype", StringComparison.OrdinalIgnoreCase)) continue;
            if (parts.Length != 2)
                return new(SettingState.Unknown, "BCDEdit returned a malformed hypervisorlaunchtype entry.");
            values.Add(parts[1]);
        }
        if (values.Count == 0)
            return new(SettingState.Enabled, "hypervisorlaunchtype is absent: Windows default is Auto. Runtime is reported separately.");
        if (values.Count != 1)
            return new(SettingState.Unknown, "BCDEdit returned multiple hypervisorlaunchtype entries.");
        var value = values[0];
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

}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Services;

public sealed class ProcessService(ILoggingService log) : IProcessService
{
    public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var allowed = executable switch
        {
            "bcdedit.exe" => arguments.SequenceEqual(["/enum", "{current}"])
                || arguments.SequenceEqual(["/set", "{current}", "hypervisorlaunchtype", "auto"])
                || arguments.SequenceEqual(["/set", "{current}", "hypervisorlaunchtype", "off"]),
            "shutdown.exe" => arguments.SequenceEqual(["/r", "/t", "0"]),
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Process command is outside the documented FACEIT Switch operations.");
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        log.Write($"Process: {executable} {string.Join(' ', arguments)}");
        using var process = Process.Start(startInfo) ?? throw new IOException($"Could not start {executable}.");
        // Drain both streams concurrently to avoid deadlock on a full pipe.
        // A WPF process normally has no console. GetOEMCP is the machine's real code page;
        // CultureInfo.TextInfo.OEMCodePage can be an unsupported placeholder such as 1.
        var consoleCodePage = GetConsoleOutputCP();
        var oemCodePage = checked((int)(consoleCodePage != 0 ? consoleCodePage : GetOEMCP()));
        var output = ReadOutputAsync(process.StandardOutput.BaseStream, oemCodePage);
        var error = ReadOutputAsync(process.StandardError.BaseStream, oemCodePage);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var stdout = await output.ConfigureAwait(false);
            var stderr = await error.ConfigureAwait(false);
            log.Write($"Process interrupted: {executable}; exit={process.ExitCode}", ex);
            throw new TimeoutException($"{executable} did not complete. Configuration may need rescanning. {stdout.Trim()} {stderr.Trim()}", ex);
        }
        var result = new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        log.Write($"Process completed: {executable}; exit={result.ExitCode}");
        if (executable == "bcdedit.exe" && !string.IsNullOrWhiteSpace(result.StandardError))
            log.Write($"BCDEdit stderr (exit {result.ExitCode}): {result.StandardError.Trim()}");
        return result;
    }

    private static async Task<string> ReadOutputAsync(Stream stream, int oemCodePage)
    {
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes).ConfigureAwait(false);
        return ProcessOutputDecoder.Decode(bytes.ToArray(), oemCodePage);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();
}

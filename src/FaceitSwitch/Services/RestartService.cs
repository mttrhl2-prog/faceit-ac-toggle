using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Services;

public sealed class RestartService(IProcessService process, ILoggingService log)
{
    // Confirmation belongs to the UI; no forced restart and no implicit /f via a positive timeout.
    public async Task<OperationResult> RestartAsync()
    {
        try
        {
            var result = await process.RunAsync("shutdown.exe", ["/r", "/t", "0"]);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Restart failed (exit {result.ExitCode}). {result.StandardOutput.Trim()} {result.StandardError.Trim()}");
            return new(true, "Windows restart requested.");
        }
        catch (Exception ex)
        {
            log.Write("Restart failed", ex);
            return new(false, ex.Message);
        }
    }
}

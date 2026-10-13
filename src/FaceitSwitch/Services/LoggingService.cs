using System.Diagnostics;
using System.IO;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly object sync = new();
    private readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FaceitSwitch", "Logs");
    public string? LastError { get; private set; }

    public void Write(string message, Exception? exception = null)
    {
        var entry = $"{DateTimeOffset.Now:O} {message}";
        if (exception is not null) entry += $" | {exception.GetType().Name}: {exception.Message}";
        lock (sync)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}.log"), entry + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LastError = $"Local logging is unavailable: {ex.Message}";
                Trace.WriteLine(LastError);
                Trace.WriteLine(entry);
            }
        }
    }
}

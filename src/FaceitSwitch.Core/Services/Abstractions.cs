using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public interface ISettingController
{
    SettingId Id { get; }
    Task<SettingReading> ReadStateAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> EnableAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> DisableAsync(CancellationToken cancellationToken = default);
}

public readonly record struct RegistryDword(bool Exists, bool IsDword, int Value)
{
    public static RegistryDword Missing => new(false, false, 0);
    public static RegistryDword Dword(int value) => new(true, true, value);
    public bool Is(int value) => Exists && IsDword && Value == value;
    public override string ToString() => !Exists ? "missing" : IsDword ? Value.ToString() : "not a DWORD";
}

public interface IRegistryService
{
    bool KeyExists(string path);
    RegistryDword ReadDword(string path, string name);
    bool ValueExists(string path, string name);
    // Return true only when the value actually changes. Exceptions reach the controller.
    bool WriteDword(string path, string name, int value);
    bool DeleteValue(string path, string name);
}

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessService
{
    Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public interface IFaceitServiceStopper
{
    Task<string?> StopAsync(CancellationToken cancellationToken = default);
}

public interface ILoggingService
{
    string? LastError => null;
    void Write(string message, Exception? exception = null);
}

public interface IRuntimeStateService
{
    Task<RuntimeState> ReadAsync(CancellationToken cancellationToken = default);
}

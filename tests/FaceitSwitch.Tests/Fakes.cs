using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Tests;

internal sealed class FakeRegistry : IRegistryService
{
    public HashSet<string> Keys { get; } = [];
    public Dictionary<(string Path, string Name), RegistryDword> Values { get; } = [];
    public List<(string Action, string Path, string Name, int? Value)> Mutations { get; } = [];
    public (string Path, string Name)? DeniedWrite { get; set; }
    public (string Path, string Name)? IgnoreWrite { get; set; }
    public bool ThrowOnRead { get; set; }
    public void Seed(string path, string name, int value) { Keys.Add(path); Values[(path, name)] = RegistryDword.Dword(value); }
    public bool KeyExists(string path) => Keys.Contains(path);
    public RegistryDword ReadDword(string path, string name) => ThrowOnRead
        ? throw new UnauthorizedAccessException("Registry read denied")
        : Values.GetValueOrDefault((path, name), RegistryDword.Missing);
    public bool ValueExists(string path, string name) => Values.ContainsKey((path, name));
    public bool WriteDword(string path, string name, int value)
    {
        if (DeniedWrite == (path, name)) throw new UnauthorizedAccessException("Registry write denied");
        if (ReadDword(path, name).Is(value)) return false;
        Mutations.Add(("write", path, name, value));
        if (IgnoreWrite != (path, name)) Seed(path, name, value);
        return true;
    }
    public bool DeleteValue(string path, string name)
    {
        if (!Values.Remove((path, name))) return false;
        Mutations.Add(("delete", path, name, null));
        return true;
    }
}

internal sealed class FakeProcess : IProcessService
{
    public string LaunchType { get; set; } = "off";
    public bool FailRead { get; set; }
    public bool FailWrite { get; set; }
    public bool IgnoreWrite { get; set; }
    public List<(string Executable, string[] Arguments)> Calls { get; } = [];
    public Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add((executable, arguments.ToArray()));
        if (arguments[0] == "/enum")
            return Task.FromResult(FailRead ? new ProcessResult(5, "", "BCD access denied")
                : new ProcessResult(0, $"Windows Boot Loader\nidentifier {{current}}\nhypervisorlaunchtype {LaunchType}\n", ""));
        if (FailWrite) return Task.FromResult(new ProcessResult(9, "operation failed", "BCD write denied"));
        if (!IgnoreWrite) LaunchType = arguments[^1];
        return Task.FromResult(new ProcessResult(0, "The operation completed successfully.", ""));
    }
}

internal sealed class FakeStopper : IFaceitServiceStopper
{
    public int Calls { get; private set; }
    public string? Warning { get; set; }
    public Task<string?> StopAsync(CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(Warning); }
}

internal sealed class FakeLog : ILoggingService
{
    public List<string> Entries { get; } = [];
    public void Write(string message, Exception? exception = null) => Entries.Add(message + exception?.Message);
}

internal sealed class Fixture
{
    public FakeRegistry Registry { get; } = new();
    public FakeProcess Process { get; } = new();
    public FakeStopper Stopper { get; } = new();
    public FakeLog Log { get; } = new();
    public ProfileService Profiles { get; }
    public Fixture()
    {
        Registry.Seed(FaceitDriverService.IommuService, "Start", 4);
        Registry.Seed(FaceitDriverService.DriverService, "Start", 4);
        Registry.Seed(FaceitDriverService.UserService, "Start", 4);
        Registry.Seed(CoreIsolationService.Configuration, "Enabled", 0);
        Registry.Seed(CoreIsolationService.Configuration, "WasEnabledBy", 0);
        Registry.Seed(DriverBlocklistService.Configuration, "VulnerableDriverBlocklistEnable", 0);
        Profiles = new([
            new FaceitDriverService(Registry, Stopper, Log), new CoreIsolationService(Registry, Log),
            new HypervisorService(Process, Log), new DriverBlocklistService(Registry, Log)
        ]);
    }
}

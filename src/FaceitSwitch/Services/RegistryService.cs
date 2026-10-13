using System.IO;
using FaceitSwitch.Core.Services;
using Microsoft.Win32;

namespace FaceitSwitch.Services;

public sealed class RegistryService : IRegistryService
{
    public bool KeyExists(string path)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(path, writable: false);
        return key is not null;
    }

    public RegistryDword ReadDword(string path, string name)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(path, writable: false);
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
            return RegistryDword.Missing;
        if (key.GetValueKind(name) != RegistryValueKind.DWord) return new(true, false, 0);
        var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is int dword ? RegistryDword.Dword(dword)
            : throw new IOException($"Could not read HKLM\\{path}\\{name} as a DWORD.");
    }

    public bool ValueExists(string path, string name)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(path, writable: false);
        return key?.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase) == true;
    }

    public bool WriteDword(string path, string name, int value)
    {
        var allowed = (path, name) switch
        {
            (FaceitDriverService.IommuService, "Start") => value is 0 or 4,
            (FaceitDriverService.DriverService, "Start") => value is 1 or 4,
            (FaceitDriverService.UserService, "Start") => value is 3 or 4,
            (FaceitDriverService.Smm, "ForceEnableIommu") => value == 2,
            (CoreIsolationService.Configuration, "Enabled") => value is 0 or 1,
            (CoreIsolationService.Configuration, "WasEnabledBy") => value is 0 or 2,
            (DriverBlocklistService.Configuration, "VulnerableDriverBlocklistEnable") => value is 0 or 1,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Registry write is outside the documented FACEIT Switch settings.");
        if (ReadDword(path, name).Is(value)) return false;
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.CreateSubKey(path, writable: true)
            ?? throw new IOException($"Could not open HKLM\\{path} for writing.");
        key.SetValue(name, value, RegistryValueKind.DWord);
        return true;
    }

    public bool DeleteValue(string path, string name)
    {
        if ((path, name) is not (FaceitDriverService.Smm, "ForceEnableIommu")
            and not (CoreIsolationService.Policy, "Enabled"))
            throw new InvalidOperationException("Registry deletion is outside the documented FACEIT Switch settings.");
        if (!ValueExists(path, name)) return false;
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(path, writable: true);
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        key.DeleteValue(name, throwOnMissingValue: false);
        return true;
    }
}

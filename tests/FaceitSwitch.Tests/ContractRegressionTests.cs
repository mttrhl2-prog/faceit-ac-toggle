using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Tests;

public sealed class ContractRegressionTests
{
    [Fact]
    public async Task FaceitEnablePrecreatesIommuAndWritesOnlyDocumentedValues()
    {
        var f = new Fixture();
        Assert.True((await f.Profiles.ApplySettingAsync(Core.Models.SettingId.FaceitDrivers, true)).Success);
        Assert.Equal(new[]
        {
            ("write", FaceitDriverService.Smm, "ForceEnableIommu", (int?)2),
            ("write", FaceitDriverService.IommuService, "Start", (int?)0),
            ("write", FaceitDriverService.DriverService, "Start", (int?)1),
            ("write", FaceitDriverService.UserService, "Start", (int?)3)
        }, f.Registry.Mutations);
    }

    [Fact]
    public async Task FaceitDisableDeletesOnlyForceValueAndNeverWritesZero()
    {
        var f = new Fixture();
        await f.Profiles.ApplySettingAsync(Core.Models.SettingId.FaceitDrivers, true);
        f.Registry.Seed(FaceitDriverService.Smm, "Unrelated", 15);
        f.Registry.Mutations.Clear();
        Assert.True((await f.Profiles.ApplySettingAsync(Core.Models.SettingId.FaceitDrivers, false)).Success);
        Assert.Equal(new[]
        {
            ("write", FaceitDriverService.IommuService, "Start", (int?)4),
            ("write", FaceitDriverService.DriverService, "Start", (int?)4),
            ("write", FaceitDriverService.UserService, "Start", (int?)4),
            ("delete", FaceitDriverService.Smm, "ForceEnableIommu", (int?)null)
        }, f.Registry.Mutations);
        Assert.True(f.Registry.ReadDword(FaceitDriverService.Smm, "Unrelated").Is(15));
        Assert.True(f.Registry.KeyExists(FaceitDriverService.Smm));
        Assert.Equal(1, f.Stopper.Calls);
    }

    [Fact]
    public void ProductionSourceDoesNotIntroduceExcludedTweaksOrCommandShells()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FaceitSwitch.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var files = Directory.GetFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Intersect(new[] { "bin", "obj" }).Any()).ToArray();
        Assert.NotEmpty(files);
        var excluded = new[] { "IOMMUFlags", "hypervisoriommupolicy", "LsaCfgFlags", "ConfigureSystemGuardLaunch",
            "EnableVirtualizationBasedSecurity", "RequirePlatformSecurityFeatures", "CredentialGuard", "SystemGuard",
            "HypervisorPlatform", "VirtualMachinePlatform", "Hyper-V", "SmartScreen", "ControlFlowGuard",
            "cmd.exe", "powershell.exe", "reg.exe" };
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var token in excluded) Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        }
    }
}

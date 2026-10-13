using System.Security.Principal;
using System.Text;
using System.Windows;
using FaceitSwitch.Core.Services;
using FaceitSwitch.Services;
using FaceitSwitch.ViewModels;
using FaceitSwitch.Views;

namespace FaceitSwitch;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || !Environment.Is64BitProcess)
        {
            MessageBox.Show("FACEIT Switch requires Windows 11 x64.", "FACEIT Switch", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            MessageBox.Show("Launch FACEIT Switch as administrator.", "FACEIT Switch", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var log = new LoggingService();
        log.Write("App launch. Configuration scan only; no startup modifications.");
        var registry = new RegistryService();
        var process = new ProcessService(log);
        var profile = new ProfileService([
            new FaceitDriverService(registry, new FaceitServiceStopper(log), log),
            new CoreIsolationService(registry, log),
            new HypervisorService(process, log),
            new DriverBlocklistService(registry, log)
        ]);
        var window = new MainWindow();
        var viewModel = new MainViewModel(profile, new RuntimeStateService(log), new RestartService(process, log), log,
            () => MessageBox.Show(window, "Restart Windows now? Save your work before continuing.", "Restart Windows",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();
    }
}

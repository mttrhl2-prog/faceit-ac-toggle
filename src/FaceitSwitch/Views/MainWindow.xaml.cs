using System.ComponentModel;
using System.Windows;
using FaceitSwitch.ViewModels;
using Wpf.Ui.Controls;

namespace FaceitSwitch.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow() => InitializeComponent();

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) await viewModel.InitializeAsync();
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        // Keep a profile write/verification sequence alive until it completes.
        if (DataContext is MainViewModel { IsBusy: true }) e.Cancel = true;
    }
}

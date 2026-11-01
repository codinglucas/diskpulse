using System.Windows;
using DiskPulse.Core;

namespace DiskPulse.App;

public partial class App : Application
{
    private MetricsService? _metrics;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new OverlayViewModel();
        var window = new OverlayWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();

        // The service samples on a background thread; hop to the UI thread to update bindings.
        _metrics = new MetricsService(
            TimeSpan.FromSeconds(1),
            DiskThresholds.Default,
            snapshot => Dispatcher.InvokeAsync(() => viewModel.Update(snapshot)),
            message => Dispatcher.InvokeAsync(() => viewModel.ShowError(message)));
        _metrics.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _metrics?.Dispose();
        base.OnExit(e);
    }
}

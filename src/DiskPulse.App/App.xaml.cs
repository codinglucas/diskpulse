using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DiskPulse.App.Resources;
using DiskPulse.Core;
using DiskPulse.Core.History;
using DiskPulse.Core.Logging;
using DiskPulse.Core.Settings;

namespace DiskPulse.App;

/// <summary>Wires the overlay, tray icon, hotkey, settings and metrics service together.</summary>
public partial class App : Application
{
    private const string SingleInstanceName = @"Local\DiskPulse.SingleInstance";

    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly SampleHistory _history = new();
    private AppSettings _settings = new();
    private CultureInfo _windowsUICulture = CultureInfo.CurrentUICulture;

    private Mutex? _singleInstance;
    private OverlayViewModel? _viewModel;
    private OverlayWindow? _overlay;
    private TrayIcon? _tray;
    private GlobalHotkey? _hotkey;
    private MetricsService? _metrics;
    private SettingsWindow? _settingsWindow;
    private MenuItem? _overlayExpandedItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second copy would fight the first over the hotkey and add a second tray icon.
        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _windowsUICulture = CultureInfo.CurrentUICulture;
        _settings = _store.Load();
        ApplyLanguage(_settings.Language);

        _viewModel = new OverlayViewModel(OverlayOptions.From(_settings.Overlay), _history);
        _overlay = new OverlayWindow(_viewModel);
        _overlay.MovedByUser += (_, _) => SavePosition();
        _overlay.Loaded += (_, _) => MonitorPlacement.Restore(_overlay, _settings.Overlay);
        _overlay.ContextMenu = BuildOverlayMenu();
        ApplyOverlaySettings();
        if (_settings.Overlay.Visible)
            _overlay.Show();

        _tray = new TrayIcon();
        _tray.ToggleOverlayRequested += (_, _) => ToggleOverlay();
        _tray.ClickThroughToggled += (_, _) => SetClickThrough(!_settings.Overlay.ClickThrough);
        _tray.ExpandedToggled += (_, _) => SetExpanded(!_settings.Overlay.Expanded);
        _tray.ExportRequested += (_, _) => ExportHistory(owner: null);
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.SetState(TrayState.Waiting, Strings.Get("Waiting"));
        UpdateMenus();

        _hotkey = new GlobalHotkey(_overlay);
        _hotkey.Pressed += (_, _) => ToggleOverlay();
        if (!RegisterHotkey(_settings.Hotkey))
            _tray.ShowNotice(Strings.Format("HotkeyInUse", _settings.Hotkey));

        // The service samples on a background thread; hop to the UI thread to update bindings.
        _metrics = new MetricsService(
            _settings.RefreshInterval,
            _settings.Thresholds.ToDiskThresholds(),
            _settings.Logging.ToLogOptions(),
            snapshot => Dispatcher.InvokeAsync(() => OnSnapshot(snapshot)),
            message => Dispatcher.InvokeAsync(() => OnMetricsError(message)),
            message => Dispatcher.InvokeAsync(() => _tray!.ShowNotice(Strings.Format("LogFailed", message))));
        _metrics.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _metrics?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }

    private void OnSnapshot(MetricsSnapshot snapshot)
    {
        _history.Add(snapshot.Readings); // Before the overlay, which draws the sparkline from it.
        _viewModel!.Update(snapshot);

        var worst = snapshot.Readings.MaxBy(r => r.Status.Health);
        if (worst is null || worst.Status.Health == DiskHealth.Ok)
        {
            _tray!.SetState(TrayState.Ok, Strings.Get("TrayAllOk"));
            return;
        }

        string problem = WarningText.Title(worst.Status.Warnings[0]);
        _tray!.SetState(TrayIcon.StateFor(worst.Status.Health),
            Strings.Format("TrayDiskWarning", DiskRowViewModel.ShortLabel(worst.Info), problem));
    }

    private void OnMetricsError(string message)
    {
        _viewModel!.ShowError(message);
        _tray!.SetState(TrayState.Error, Strings.Get("TrayError"));
    }

    private void ToggleOverlay()
    {
        var overlay = _overlay!;
        if (overlay.IsVisible)
            overlay.Hide();
        else
            overlay.Show();

        _settings.Overlay.Visible = overlay.IsVisible;
        Save();
        UpdateMenus();
    }

    private void SetClickThrough(bool enabled)
    {
        _settings.Overlay.ClickThrough = enabled;
        _overlay!.SetClickThrough(enabled);
        Save();
        UpdateMenus();
        if (enabled)
            _tray!.ShowNotice(Strings.Get("ClickThroughOnHint"));
    }

    private void SetExpanded(bool expanded)
    {
        _settings.Overlay.Expanded = expanded;
        ApplyOverlaySettings();
        Save();
        UpdateMenus();
    }

    /// <summary>Saves the in-memory history (up to 10 minutes, every disk) to a CSV file the user picks.</summary>
    private void ExportHistory(Window? owner)
    {
        var readings = _history.All();
        if (readings.Count == 0)
        {
            MessageBox.Show(Strings.Get("Export_Empty"), "DiskPulse", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.Get("Export_Title"),
            Filter = Strings.Get("Export_Filter"),
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"DiskPulse_{readings[^1].Sample.Timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        bool chosen = owner is null ? dialog.ShowDialog() == true : dialog.ShowDialog(owner) == true;
        if (!chosen)
            return;

        try
        {
            int rows;
            using (var writer = new System.IO.StreamWriter(dialog.FileName, append: false, CsvFormat.Encoding))
                rows = CsvFormat.Write(writer, readings);
            _tray!.ShowNotice(Strings.Format("Export_Done", rows, dialog.FileName));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(Strings.Format("Export_Failed", ex.Message), "DiskPulse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings.Clone(), StartupRegistration.IsEnabled());
        _settingsWindow.Saved += (_, e) => ApplySettings(e.Settings, e.StartWithWindows);
        _settingsWindow.ExportRequested += (_, _) => ExportHistory(_settingsWindow);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySettings(AppSettings updated, bool startWithWindows)
    {
        updated.Normalize();

        // Things that may have changed while the window was open belong to the live settings.
        updated.Overlay.Visible = _settings.Overlay.Visible;
        updated.Overlay.LastMonitor = _settings.Overlay.LastMonitor;
        updated.Overlay.Positions = _settings.Overlay.Positions;

        if (updated.Hotkey != _settings.Hotkey && !RegisterHotkey(updated.Hotkey))
        {
            MessageBox.Show(Strings.Format("HotkeyInUse", updated.Hotkey), "DiskPulse", MessageBoxButton.OK, MessageBoxImage.Warning);
            updated.Hotkey = _settings.Hotkey;
            RegisterHotkey(updated.Hotkey);
        }

        bool languageChanged = updated.Language != _settings.Language;
        _settings = updated;

        if (languageChanged)
        {
            ApplyLanguage(_settings.Language);
            _tray!.RefreshTexts();
            _overlay!.ContextMenu = BuildOverlayMenu();
        }

        ApplyOverlaySettings();
        _metrics!.Update(_settings.RefreshInterval, _settings.Thresholds.ToDiskThresholds(), _settings.Logging.ToLogOptions());
        if (startWithWindows != StartupRegistration.IsEnabled())
            StartupRegistration.SetEnabled(startWithWindows);
        Save();
        UpdateMenus();
    }

    private void ApplyOverlaySettings()
    {
        var overlay = _overlay!;
        overlay.Opacity = _settings.Overlay.Opacity;
        overlay.FontSize = _settings.Overlay.FontSize;
        overlay.SetClickThrough(_settings.Overlay.ClickThrough);
        _viewModel!.SetOptions(OverlayOptions.From(_settings.Overlay));
    }

    /// <summary>"" follows Windows. Number formats keep following the Windows region either way.</summary>
    private void ApplyLanguage(string language)
    {
        var culture = language.Length == 0 ? _windowsUICulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private bool RegisterHotkey(string text) =>
        HotkeyGesture.TryParse(text, out var gesture) && _hotkey!.Register(gesture);

    private void SavePosition()
    {
        if (MonitorPlacement.Capture(_overlay!) is not { } placement)
            return;

        _settings.Overlay.LastMonitor = placement.Monitor;
        _settings.Overlay.Positions[placement.Monitor] = placement.Offset;
        Save();
    }

    private void Save()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // Not worth interrupting the user; the settings still apply for this run.
        }
    }

    private void UpdateMenus()
    {
        bool visible = _overlay?.IsVisible ?? _settings.Overlay.Visible;
        _tray?.SetMenuState(visible, _settings.Overlay.ClickThrough, _settings.Overlay.Expanded);
        if (_overlayExpandedItem is not null)
            _overlayExpandedItem.IsChecked = _settings.Overlay.Expanded;
    }

    /// <summary>Right-click menu on the overlay itself. Unreachable while click-through is on; the tray menu covers that.</summary>
    private ContextMenu BuildOverlayMenu()
    {
        var clickThrough = new MenuItem { Header = Strings.Get("MenuClickThrough") };
        clickThrough.Click += (_, _) => SetClickThrough(true);
        _overlayExpandedItem = new MenuItem { Header = Strings.Get("MenuExpanded"), IsCheckable = true, IsChecked = _settings.Overlay.Expanded };
        _overlayExpandedItem.Click += (_, _) => SetExpanded(!_settings.Overlay.Expanded);
        var export = new MenuItem { Header = Strings.Get("MenuExport") };
        export.Click += (_, _) => ExportHistory(owner: null);
        var settings = new MenuItem { Header = Strings.Get("MenuSettings") };
        settings.Click += (_, _) => OpenSettings();
        var hide = new MenuItem { Header = Strings.Get("MenuHideOverlay") };
        hide.Click += (_, _) => ToggleOverlay();
        var exit = new MenuItem { Header = Strings.Get("MenuExit") };
        exit.Click += (_, _) => Shutdown();

        return new ContextMenu { Items = { clickThrough, _overlayExpandedItem, export, settings, hide, new Separator(), exit } };
    }
}

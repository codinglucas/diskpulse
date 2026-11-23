using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskPulse.App.Resources;
using DiskPulse.Core.Settings;

namespace DiskPulse.App;

public sealed class SettingsSavedEventArgs(AppSettings settings, bool startWithWindows) : EventArgs
{
    public AppSettings Settings { get; } = settings;
    public bool StartWithWindows { get; } = startWithWindows;
}

/// <summary>
/// Edits a copy of the settings. Nothing changes until Save, which raises <see cref="Saved"/>;
/// the app then normalizes and applies the result.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly double[] RefreshChoices = [0.5, 1, 2, 3, 5];

    private readonly AppSettings _original;
    private HotkeyGesture _hotkey;

    public SettingsWindow(AppSettings settings, bool startWithWindows)
    {
        InitializeComponent();
        _original = settings;
        _hotkey = HotkeyGesture.Default;

        LanguageBox.Items.Add(new ComboBoxItem { Content = Strings.Get("Settings_LanguageSystem"), Tag = "" });
        LanguageBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        LanguageBox.Items.Add(new ComboBoxItem { Content = "Português (Brasil)", Tag = "pt-BR" });
        foreach (double seconds in RefreshChoices)
            RefreshBox.Items.Add(new ComboBoxItem { Content = Strings.Format("Settings_Seconds", seconds), Tag = seconds });

        Fill(settings, startWithWindows);
    }

    public event EventHandler<SettingsSavedEventArgs>? Saved;

    private void Fill(AppSettings s, bool startWithWindows)
    {
        Select(LanguageBox, s.Language);
        _hotkey = HotkeyGesture.TryParse(s.Hotkey, out var gesture) ? gesture : HotkeyGesture.Default;
        HotkeyBox.Text = _hotkey.ToString();
        StartWithWindowsBox.IsChecked = startWithWindows;

        var o = s.Overlay;
        ClickThroughBox.IsChecked = o.ClickThrough;
        OpacitySlider.Value = Math.Round(o.Opacity * 100);
        FontSizeSlider.Value = o.FontSize;
        Select(RefreshBox, RefreshChoices.MinBy(c => Math.Abs(c - s.RefreshIntervalSeconds)));
        ShowResponseBox.IsChecked = o.ShowResponseTime;
        ShowActiveBox.IsChecked = o.ShowActiveTime;
        ShowQueueBox.IsChecked = o.ShowQueueLength;
        ShowThroughputBox.IsChecked = o.ShowThroughput;

        var t = s.Thresholds;
        SetNumber(SlowMsBox, t.SlowResponseMs);
        SetNumber(SlowSecondsBox, t.SlowResponseSeconds);
        SetNumber(VerySlowMsBox, t.VerySlowResponseMs);
        SetNumber(VerySlowSecondsBox, t.VerySlowResponseSeconds);
        SetNumber(QueueBox, t.HighQueueLength);
        SetNumber(QueueMsBox, t.HighQueueMinResponseMs);
        SetNumber(QueueSecondsBox, t.HighQueueSeconds);
        SetNumber(BusyActiveBox, t.BusyActivePercent);
        SetNumber(BusyMBpsBox, t.BusyMaxThroughputMBps);
        SetNumber(BusySecondsBox, t.BusySeconds);
    }

    /// <summary>Reads the form into a copy of the original settings. Throws FormatException with a user-facing message.</summary>
    private AppSettings Read()
    {
        var s = _original.Clone();
        s.Language = (string)((ComboBoxItem)LanguageBox.SelectedItem).Tag;
        s.Hotkey = _hotkey.ToString();
        s.RefreshIntervalSeconds = (double)((ComboBoxItem)RefreshBox.SelectedItem).Tag;

        var o = s.Overlay;
        o.ClickThrough = ClickThroughBox.IsChecked == true;
        o.Opacity = OpacitySlider.Value / 100;
        o.FontSize = FontSizeSlider.Value;
        o.ShowResponseTime = ShowResponseBox.IsChecked == true;
        o.ShowActiveTime = ShowActiveBox.IsChecked == true;
        o.ShowQueueLength = ShowQueueBox.IsChecked == true;
        o.ShowThroughput = ShowThroughputBox.IsChecked == true;

        var t = s.Thresholds;
        t.SlowResponseMs = ReadNumber(SlowMsBox);
        t.SlowResponseSeconds = ReadNumber(SlowSecondsBox);
        t.VerySlowResponseMs = ReadNumber(VerySlowMsBox);
        t.VerySlowResponseSeconds = ReadNumber(VerySlowSecondsBox);
        t.HighQueueLength = ReadNumber(QueueBox);
        t.HighQueueMinResponseMs = ReadNumber(QueueMsBox);
        t.HighQueueSeconds = ReadNumber(QueueSecondsBox);
        t.BusyActivePercent = ReadNumber(BusyActiveBox);
        t.BusyMaxThroughputMBps = ReadNumber(BusyMBpsBox);
        t.BusySeconds = ReadNumber(BusySecondsBox);
        return s;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        AppSettings updated;
        try
        {
            updated = Read();
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Saved?.Invoke(this, new SettingsSavedEventArgs(updated, StartWithWindowsBox.IsChecked == true));
        Close();
    }

    /// <summary>Resets the form only. Language and "start with Windows" are kept; Save still applies.</summary>
    private void OnRestoreDefaults(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings { Language = (string)((ComboBoxItem)LanguageBox.SelectedItem).Tag };
        Fill(defaults, StartWithWindowsBox.IsChecked == true);
    }

    /// <summary>Captures a shortcut: the user clicks the box and presses the keys they want.</summary>
    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab)
            return; // Keep keyboard navigation working.
        e.Handled = true;

        if (ToGestureKey(key) is not { } name)
            return; // A lone modifier, or a key a hotkey can't use.

        var modifiers = Keyboard.Modifiers;
        var gestureModifiers =
            (modifiers.HasFlag(ModifierKeys.Control) ? HotkeyModifiers.Control : 0) |
            (modifiers.HasFlag(ModifierKeys.Alt) ? HotkeyModifiers.Alt : 0) |
            (modifiers.HasFlag(ModifierKeys.Shift) ? HotkeyModifiers.Shift : 0) |
            (modifiers.HasFlag(ModifierKeys.Windows) ? HotkeyModifiers.Windows : 0);

        if (HotkeyGesture.TryParse(new HotkeyGesture(gestureModifiers, name).ToString(), out var gesture))
        {
            _hotkey = gesture;
            HotkeyBox.Text = gesture.ToString();
        }
        else
        {
            MessageBox.Show(this, Strings.Get("Settings_InvalidHotkey"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static string? ToGestureKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture),
        >= Key.NumPad0 and <= Key.NumPad9 => ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Space or Key.Insert or Key.Delete or Key.Home or Key.End or Key.PageUp or Key.PageDown
            or Key.Up or Key.Down or Key.Left or Key.Right => key.ToString(),
        _ => null,
    };

    private static void Select(ComboBox box, object tag) =>
        box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, tag)) ?? box.Items[0];

    private static void SetNumber(TextBox box, double value) => box.Text = value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Accepts the user's own decimal separator ("0,5" on pt-BR) and also "0.5".</summary>
    private static double ReadNumber(TextBox box)
    {
        string text = box.Text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return value;

        box.Focus();
        box.SelectAll();
        throw new FormatException(Strings.Format("Settings_InvalidNumber", text));
    }
}

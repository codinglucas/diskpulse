using DiskPulse.App.Native;
using DiskPulse.App.Resources;
using DiskPulse.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DiskPulse.App;

internal enum TrayState
{
    Waiting,
    Ok,
    Warning,
    Critical,
    Error,
}

/// <summary>
/// Notification-area icon: a dot in the color of the worst disk, a summary tooltip, and the
/// menu that stays reachable while the overlay is hidden or click-through.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    // Windows truncates longer notification-icon tooltips.
    private const int MaxTooltipLength = 63;

    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleOverlay = new();
    private readonly Forms.ToolStripMenuItem _clickThrough = new();
    private readonly Forms.ToolStripMenuItem _expanded = new();
    private readonly Forms.ToolStripMenuItem _export = new();
    private readonly Forms.ToolStripMenuItem _settings = new();
    private readonly Forms.ToolStripMenuItem _exit = new();
    private readonly Dictionary<TrayState, Drawing.Icon> _icons;
    private bool _overlayVisible = true;

    public TrayIcon()
    {
        _icons = new()
        {
            [TrayState.Waiting] = CreateDot(Drawing.Color.FromArgb(0x9E, 0x9E, 0x9E)),
            [TrayState.Ok] = CreateDot(Drawing.Color.FromArgb(0x4C, 0xAF, 0x50)),
            [TrayState.Warning] = CreateDot(Drawing.Color.FromArgb(0xFF, 0xC1, 0x07)),
            [TrayState.Critical] = CreateDot(Drawing.Color.FromArgb(0xF4, 0x43, 0x36)),
            [TrayState.Error] = CreateDot(Drawing.Color.FromArgb(0x60, 0x60, 0x60)),
        };

        _toggleOverlay.Click += (_, _) => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty);
        _clickThrough.Click += (_, _) => ClickThroughToggled?.Invoke(this, EventArgs.Empty);
        _expanded.Click += (_, _) => ExpandedToggled?.Invoke(this, EventArgs.Empty);
        _export.Click += (_, _) => ExportRequested?.Invoke(this, EventArgs.Empty);
        _settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(new Forms.ToolStripItem[] { _toggleOverlay, _clickThrough, _expanded, _export, _settings, new Forms.ToolStripSeparator(), _exit });

        _icon = new Forms.NotifyIcon
        {
            Icon = _icons[TrayState.Waiting],
            Text = "DiskPulse",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                ToggleOverlayRequested?.Invoke(this, EventArgs.Empty);
        };

        RefreshTexts();
    }

    public event EventHandler? ToggleOverlayRequested;
    public event EventHandler? ClickThroughToggled;
    public event EventHandler? ExpandedToggled;
    public event EventHandler? ExportRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void SetState(TrayState state, string summary)
    {
        _icon.Icon = _icons[state];
        string text = $"DiskPulse - {summary}";
        _icon.Text = text.Length <= MaxTooltipLength ? text : text[..(MaxTooltipLength - 1)] + "…";
    }

    public void SetMenuState(bool overlayVisible, bool clickThrough, bool expanded)
    {
        _overlayVisible = overlayVisible;
        _toggleOverlay.Text = Strings.Get(overlayVisible ? "MenuHideOverlay" : "MenuShowOverlay");
        _clickThrough.Checked = clickThrough;
        _expanded.Checked = expanded;
    }

    /// <summary>Re-reads menu labels, after the language changes.</summary>
    public void RefreshTexts()
    {
        _clickThrough.Text = Strings.Get("MenuClickThrough");
        _expanded.Text = Strings.Get("MenuExpanded");
        _export.Text = Strings.Get("MenuExport");
        _settings.Text = Strings.Get("MenuSettings");
        _exit.Text = Strings.Get("MenuExit");
        SetMenuState(_overlayVisible, _clickThrough.Checked, _expanded.Checked);
    }

    public void ShowNotice(string text) => _icon.ShowBalloonTip(5000, "DiskPulse", text, Forms.ToolTipIcon.Info);

    public static TrayState StateFor(DiskHealth worst) => worst switch
    {
        DiskHealth.Critical => TrayState.Critical,
        DiskHealth.Warning => TrayState.Warning,
        _ => TrayState.Ok,
    };

    public void Dispose()
    {
        // Hide first, or a dead icon lingers in the notification area until the mouse passes over it.
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        foreach (var icon in _icons.Values)
            icon.Dispose();
    }

    private static Drawing.Icon CreateDot(Drawing.Color color)
    {
        int size = Forms.SystemInformation.SmallIconSize.Width;
        using var bitmap = new Drawing.Bitmap(size, size);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Drawing.Color.Transparent);
            var bounds = new Drawing.RectangleF(1, 1, size - 3, size - 3);
            using var fill = new Drawing.SolidBrush(color);
            using var outline = new Drawing.Pen(Drawing.Color.FromArgb(160, 0, 0, 0));
            graphics.FillEllipse(fill, bounds);
            graphics.DrawEllipse(outline, bounds);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            // Clone owns its own copy, so the GDI handle from GetHicon can be released right away.
            return (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}

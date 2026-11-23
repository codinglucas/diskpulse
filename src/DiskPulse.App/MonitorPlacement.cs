using System.Windows;
using System.Windows.Interop;
using DiskPulse.App.Native;
using DiskPulse.Core.Settings;

namespace DiskPulse.App;

/// <summary>
/// Saves and restores the overlay position per monitor. Everything is in physical pixels relative
/// to the monitor's work area, which stays correct across DPI changes and taskbar moves.
/// </summary>
internal static class MonitorPlacement
{
    /// <summary>The monitor the window is on and its offset from that monitor's work area.</summary>
    public static (string Monitor, OverlayPosition Offset)? Capture(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
            return null;

        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (!NativeMethods.TryGetMonitorInfo(monitor, out var info))
            return null;

        return (info.DeviceName, new OverlayPosition { X = rect.Left - info.WorkArea.Left, Y = rect.Top - info.WorkArea.Top });
    }

    /// <summary>
    /// Moves the window to its saved spot on the last monitor it was on, or on any other monitor it
    /// remembers if that one is unplugged. Keeps the window inside the work area. Returns false when
    /// nothing saved applies, leaving the window where it is.
    /// </summary>
    public static bool Restore(Window window, OverlaySettings settings)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || settings.Positions.Count == 0)
            return false;

        var workAreas = GetWorkAreas();
        string? monitor = settings.LastMonitor is { } last && workAreas.ContainsKey(last) && settings.Positions.ContainsKey(last)
            ? last
            : settings.Positions.Keys.FirstOrDefault(workAreas.ContainsKey);
        if (monitor is null)
            return false;

        var area = workAreas[monitor];
        var offset = settings.Positions[monitor];
        NativeMethods.GetWindowRect(hwnd, out var rect);

        int x = Math.Clamp(area.Left + offset.X, area.Left, Math.Max(area.Left, area.Right - rect.Width));
        int y = Math.Clamp(area.Top + offset.Y, area.Top, Math.Max(area.Top, area.Bottom - rect.Height));
        return NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    /// <summary>Whether the window's top-left corner is in the right and/or bottom half of its monitor.</summary>
    public static (bool Right, bool Bottom) GetHalf(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
            return (false, false);

        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (!NativeMethods.TryGetMonitorInfo(monitor, out var info))
            return (false, false);

        var area = info.WorkArea;
        return (rect.Left > area.Left + (area.Width / 2), rect.Top > area.Top + (area.Height / 2));
    }

    private static Dictionary<string, NativeMethods.Rect> GetWorkAreas()
    {
        var areas = new Dictionary<string, NativeMethods.Rect>(StringComparer.OrdinalIgnoreCase);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
        {
            if (NativeMethods.TryGetMonitorInfo(monitor, out var info))
                areas[info.DeviceName] = info.WorkArea;
            return true;
        }, IntPtr.Zero);
        return areas;
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DiskPulse.App.Native;
using DiskPulse.Core.Settings;

namespace DiskPulse.App;

/// <summary>A system-wide shortcut registered on a window's handle, raising <see cref="Pressed"/>.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0xD15C;

    private readonly HwndSource _source;
    private bool _registered;

    public GlobalHotkey(Window window)
    {
        // Works while the window is hidden too: the handle exists even if it was never shown.
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
    }

    public event EventHandler? Pressed;

    public HotkeyGesture? Current { get; private set; }

    /// <summary>Returns false when another program already owns the shortcut.</summary>
    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        if (!TryGetVirtualKey(gesture.Key, out uint virtualKey))
            return false;

        _registered = NativeMethods.RegisterHotKey(
            _source.Handle, HotkeyId, (uint)gesture.Modifiers | NativeMethods.ModNoRepeat, virtualKey);
        Current = _registered ? gesture : null;
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
            return;
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
        Current = null;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }

    /// <summary>Maps a <see cref="HotkeyGesture.Key"/> such as "D", "5" or "F9" to a Win32 virtual-key code.</summary>
    internal static bool TryGetVirtualKey(string key, out uint virtualKey)
    {
        string name = key.Length == 1 && char.IsAsciiDigit(key[0]) ? "D" + key : key;
        virtualKey = Enum.TryParse(name, ignoreCase: true, out Key wpfKey) ? (uint)KeyInterop.VirtualKeyFromKey(wpfKey) : 0;
        return virtualKey != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }
}

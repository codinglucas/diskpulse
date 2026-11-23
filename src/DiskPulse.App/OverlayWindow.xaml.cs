using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DiskPulse.App.Native;

namespace DiskPulse.App;

public partial class OverlayWindow : Window
{
    private const double ScreenMargin = 16;

    private bool _clickThrough;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Default spot: top-left of the primary monitor. A saved position replaces it once loaded.
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ScreenMargin;
        Top = workArea.Top + ScreenMargin;

        SourceInitialized += (_, _) => ApplyExtendedStyle();
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Raised after the user finishes dragging the overlay.</summary>
    public event EventHandler? MovedByUser;

    /// <summary>When on, clicks go to whatever is underneath, so the overlay never blocks a game.</summary>
    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        ApplyExtendedStyle();
    }

    private void ApplyExtendedStyle()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // Tool window keeps the overlay out of Alt+Tab; layered + transparent lets clicks through.
        long style = NativeMethods.GetExtendedStyle(hwnd) | NativeMethods.WsExToolWindow | NativeMethods.WsExLayered;
        style = _clickThrough ? style | NativeMethods.WsExTransparent : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetExtendedStyle(hwnd, style);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        DragMove(); // Returns when the button is released.
        MovedByUser?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Keeps the edge nearest the screen corner fixed as rows appear or the font changes, so an
    /// overlay parked at the right or bottom edge grows inward instead of off the screen.
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || e.PreviousSize.Width == 0)
            return;

        var (right, bottom) = MonitorPlacement.GetHalf(this);
        if (right && e.WidthChanged)
            Left -= e.NewSize.Width - e.PreviousSize.Width;
        if (bottom && e.HeightChanged)
            Top -= e.NewSize.Height - e.PreviousSize.Height;
    }
}

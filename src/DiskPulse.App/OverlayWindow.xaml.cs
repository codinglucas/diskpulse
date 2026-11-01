using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskPulse.App.Resources;

namespace DiskPulse.App;

public partial class OverlayWindow : Window
{
    private const double ScreenMargin = 16;

    public OverlayWindow()
    {
        InitializeComponent();

        // Top-left of the primary monitor's work area. Remembering the position comes with settings.
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ScreenMargin;
        Top = workArea.Top + ScreenMargin;

        // Until the tray icon exists, right-click is the way to close the overlay.
        var exit = new MenuItem { Header = Strings.Get("MenuExit") };
        exit.Click += (_, _) => Application.Current.Shutdown();
        ContextMenu = new ContextMenu { Items = { exit } };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}

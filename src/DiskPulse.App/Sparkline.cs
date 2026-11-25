using System.Windows;
using System.Windows.Media;
using DiskPulse.Core.History;

namespace DiskPulse.App;

/// <summary>
/// Draws a <see cref="SparklineData"/>: the response-time line, plus a dashed line at the
/// "slow" threshold. Redrawn once per tick, which costs far less than a charting library.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(SparklineData), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Pen GuidePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xC1, 0x07))), 1)
    {
        DashStyle = Freeze(new DashStyle([3, 3], 0)),
    });

    private static readonly Pen BaselinePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))), 1));

    public SparklineData? Data
    {
        get => (SparklineData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // Half-pixel offsets keep 1 px lines crisp.
        dc.DrawLine(BaselinePen, new Point(0, height - 0.5), new Point(width, height - 0.5));

        if (Data is not { } data)
            return;

        if (data.ThresholdY is { } thresholdY)
        {
            double y = Math.Round(ToY(thresholdY, height)) + 0.5;
            dc.DrawLine(GuidePen, new Point(0, y), new Point(width, y));
        }

        var pen = Freeze(new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round });
        foreach (var segment in data.Segments)
        {
            if (segment.Count == 1)
            {
                var p = segment[0];
                dc.DrawEllipse(Stroke, null, new Point(p.X * width, ToY(p.Y, height)), 1.5, 1.5);
                continue;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(segment[0].X * width, ToY(segment[0].Y, height)), isFilled: false, isClosed: false);
                for (int i = 1; i < segment.Count; i++)
                    context.LineTo(new Point(segment[i].X * width, ToY(segment[i].Y, height)), isStroked: true, isSmoothJoin: true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    /// <summary>Leaves room for the pen at the top and bottom edges.</summary>
    private static double ToY(double y, double height) => 1 + (1 - y) * (height - 2);

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

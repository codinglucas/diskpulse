using System.Text;
using System.Windows.Media;
using DiskPulse.App.Resources;
using DiskPulse.Core;
using DiskPulse.Core.History;
using DiskPulse.Core.Settings;

namespace DiskPulse.App;

/// <summary>Which metrics the overlay shows, from <see cref="OverlaySettings"/>.</summary>
public sealed record OverlayOptions(bool ResponseTime, bool ActiveTime, bool QueueLength, bool Throughput, bool Expanded)
{
    public static OverlayOptions From(OverlaySettings settings) =>
        new(settings.ShowResponseTime, settings.ShowActiveTime, settings.ShowQueueLength, settings.ShowThroughput, settings.Expanded);
}

/// <summary>One disk in the overlay. Updated in place each tick so an open tooltip stays open.</summary>
public sealed class DiskRowViewModel(int diskNumber) : ObservableObject
{
    private const string MetricSeparator = "   ";

    private string _label = "";
    private string _metrics = "";
    private string _statusText = "";
    private string _explanation = "";
    private Brush _statusBrush = HealthBrushes.Ok;
    private SparklineData? _sparkline;
    private string _sparklineCaption = "";

    public int DiskNumber { get; } = diskNumber;

    public string Label { get => _label; private set => SetField(ref _label, value); }

    public string Metrics
    {
        get => _metrics;
        private set
        {
            if (SetField(ref _metrics, value))
                OnPropertyChanged(nameof(HasMetrics));
        }
    }

    /// <summary>False when every metric is turned off in settings; the row then shows only its label.</summary>
    public bool HasMetrics => Metrics.Length > 0;

    /// <summary>Warning titles, empty when the disk is healthy.</summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetField(ref _statusText, value))
                OnPropertyChanged(nameof(HasWarning));
        }
    }

    public bool HasWarning => StatusText.Length > 0;

    /// <summary>Tooltip text: what each warning means, plus the read/write split.</summary>
    public string Explanation { get => _explanation; private set => SetField(ref _explanation, value); }

    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }

    /// <summary>Last 60 seconds of response time; null in compact mode, which hides the graph.</summary>
    public SparklineData? Sparkline
    {
        get => _sparkline;
        private set
        {
            if (SetField(ref _sparkline, value))
                OnPropertyChanged(nameof(HasSparkline));
        }
    }

    public bool HasSparkline => Sparkline is not null;

    public string SparklineCaption { get => _sparklineCaption; private set => SetField(ref _sparklineCaption, value); }

    public void Update(DiskReading reading, DiskThresholds thresholds, OverlayOptions options, SparklineData? sparkline)
    {
        var (sample, info, status) = reading;

        Label = FormatLabel(info);
        Metrics = FormatMetrics(sample, options);
        StatusText = string.Join(" · ", status.Warnings.Select(WarningText.Title));
        StatusBrush = HealthBrushes.For(status.Health);
        Sparkline = sparkline;
        SparklineCaption = sparkline is null ? "" : Strings.Format("Sparkline_Caption", sparkline.Peak, thresholds.SlowResponseMs);

        var explanation = new StringBuilder();
        if (status.Warnings.Count == 0)
            explanation.AppendLine(Strings.Get("StatusOk"));
        foreach (var warning in status.Warnings)
        {
            explanation.AppendLine(WarningText.Title(warning));
            explanation.AppendLine(WarningText.Detail(warning, thresholds));
            explanation.AppendLine();
        }
        if (status.Warnings.Count == 0)
            explanation.AppendLine();
        explanation.Append(Strings.Format("LatencyDetail", sample.ReadResponseMs, sample.WriteResponseMs));
        Explanation = explanation.ToString();
    }

    /// <summary>Short label for the tray tooltip, e.g. "Disk 1".</summary>
    public static string ShortLabel(DiskInfo info) => Strings.Format("DiskLabel", info.Number);

    private static string FormatMetrics(DiskSample sample, OverlayOptions options)
    {
        var parts = new List<string>(4);
        if (options.ResponseTime)
            parts.Add(Strings.Format("Metric_Response", sample.ResponseMs));
        if (options.ActiveTime)
            parts.Add(Strings.Format("Metric_Active", sample.ActivePercent));
        if (options.QueueLength)
            parts.Add(Strings.Format("Metric_Queue", sample.QueueLength));
        if (options.Throughput)
            parts.Add(Strings.Format("Metric_Throughput", sample.ReadMBps, sample.WriteMBps));
        return string.Join(MetricSeparator, parts);
    }

    private static string FormatLabel(DiskInfo info)
    {
        var parts = new List<string> { ShortLabel(info) };
        if (info.DriveLetters.Count > 0)
            parts.Add(string.Join(' ', info.DriveLetters.Select(l => $"{l}:")));
        parts.Add(info.Model == DiskInfo.UnknownModel ? Strings.Get("UnknownModel") : info.Model);
        parts.Add(info.Type == DiskType.Unknown ? Strings.Get("UnknownType") : info.Type.ToDisplayName());
        return string.Join(" · ", parts);
    }
}

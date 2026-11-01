using System.Text;
using System.Windows.Media;
using DiskPulse.App.Resources;
using DiskPulse.Core;

namespace DiskPulse.App;

/// <summary>One disk in the overlay. Updated in place each tick so an open tooltip stays open.</summary>
public sealed class DiskRowViewModel(int diskNumber) : ObservableObject
{
    private string _label = "";
    private string _metrics = "";
    private string _statusText = "";
    private string _explanation = "";
    private Brush _statusBrush = HealthBrushes.Ok;

    public int DiskNumber { get; } = diskNumber;

    public string Label { get => _label; private set => SetField(ref _label, value); }

    public string Metrics { get => _metrics; private set => SetField(ref _metrics, value); }

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

    public void Update(DiskReading reading, DiskThresholds thresholds)
    {
        var (sample, info, status) = reading;

        Label = FormatLabel(info);
        Metrics = Strings.Format("MetricsLine",
            sample.ResponseMs, sample.ActivePercent, sample.QueueLength, sample.ReadMBps, sample.WriteMBps);
        StatusText = string.Join(" · ", status.Warnings.Select(WarningText.Title));
        StatusBrush = HealthBrushes.For(status.Health);

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

    private static string FormatLabel(DiskInfo info)
    {
        var parts = new List<string> { Strings.Format("DiskLabel", info.Number) };
        if (info.DriveLetters.Count > 0)
            parts.Add(string.Join(' ', info.DriveLetters.Select(l => $"{l}:")));
        parts.Add(info.Model == DiskInfo.UnknownModel ? Strings.Get("UnknownModel") : info.Model);
        parts.Add(info.Type == DiskType.Unknown ? Strings.Get("UnknownType") : info.Type.ToDisplayName());
        return string.Join(" · ", parts);
    }
}

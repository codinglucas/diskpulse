using System.Collections.ObjectModel;
using System.Windows.Media;
using DiskPulse.App.Resources;

namespace DiskPulse.App;

public sealed class OverlayViewModel(OverlayOptions options) : ObservableObject
{
    private string? _message = Strings.Get("Waiting");
    private Brush _messageBrush = HealthBrushes.Muted;
    private MetricsSnapshot? _last;

    public ObservableCollection<DiskRowViewModel> Disks { get; } = new();

    public OverlayOptions Options { get; private set; } = options;

    /// <summary>Shown instead of, or above, the disk list: waiting, no disks, or an error.</summary>
    public string? Message
    {
        get => _message;
        private set
        {
            if (SetField(ref _message, value))
                OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public Brush MessageBrush { get => _messageBrush; private set => SetField(ref _messageBrush, value); }

    public void Update(MetricsSnapshot snapshot)
    {
        _last = snapshot;
        var readings = snapshot.Readings;
        Message = readings.Count == 0 ? Strings.Get("NoDisks") : null;
        MessageBrush = HealthBrushes.Muted;

        // Rebuild rows only when disks come or go; otherwise update in place.
        if (!readings.Select(r => r.Sample.DiskNumber).SequenceEqual(Disks.Select(d => d.DiskNumber)))
        {
            Disks.Clear();
            foreach (var reading in readings)
                Disks.Add(new DiskRowViewModel(reading.Sample.DiskNumber));
        }

        for (int i = 0; i < readings.Count; i++)
            Disks[i].Update(readings[i], snapshot.Thresholds, Options);
    }

    /// <summary>Applies new display options (or a new language) at once, without waiting for the next tick.</summary>
    public void SetOptions(OverlayOptions options)
    {
        Options = options;
        if (_last is not null && Message is null)
            Update(_last);
        else if (_last is null)
            Message = Strings.Get("Waiting");
    }

    public void ShowError(string message)
    {
        Disks.Clear();
        Message = message;
        MessageBrush = HealthBrushes.Critical;
    }
}

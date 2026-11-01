using System.Collections.ObjectModel;
using System.Windows.Media;
using DiskPulse.App.Resources;

namespace DiskPulse.App;

public sealed class OverlayViewModel : ObservableObject
{
    private string? _message = Strings.Get("Waiting");
    private Brush _messageBrush = HealthBrushes.Muted;

    public ObservableCollection<DiskRowViewModel> Disks { get; } = new();

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
            Disks[i].Update(readings[i], snapshot.Thresholds);
    }

    public void ShowError(string message)
    {
        Disks.Clear();
        Message = message;
        MessageBrush = HealthBrushes.Critical;
    }
}

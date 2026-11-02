using DiskPulse.App.Resources;
using DiskPulse.Core;

namespace DiskPulse.App;

/// <summary>Plain-language title and explanation for each warning, using the active thresholds.</summary>
internal static class WarningText
{
    public static string Title(DiskWarning warning) => Strings.Get($"Warning_{warning}_Title");

    public static string Detail(DiskWarning warning, DiskThresholds t) => warning switch
    {
        DiskWarning.SlowResponse =>
            Strings.Format("Warning_SlowResponse_Detail", t.SlowResponseMs, t.SlowResponseDuration.TotalSeconds),
        DiskWarning.HighQueue =>
            Strings.Format("Warning_HighQueue_Detail", t.HighQueueLength, t.HighQueueDuration.TotalSeconds, t.HighQueueMinResponseMs),
        DiskWarning.VerySlowResponse =>
            Strings.Format("Warning_VerySlowResponse_Detail", t.VerySlowResponseMs, t.VerySlowResponseDuration.TotalSeconds),
        DiskWarning.BusyNotMoving =>
            Strings.Format("Warning_BusyNotMoving_Detail", t.BusyActivePercent, t.BusyMaxThroughputMBps, t.BusyDuration.TotalSeconds),
        _ => Title(warning),
    };
}

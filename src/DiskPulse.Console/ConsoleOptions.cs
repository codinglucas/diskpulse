using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DiskPulse.ConsoleApp;

internal enum ConsoleMode
{
    Live,
    Once,
    Diagnostics,
}

internal sealed record ConsoleOptions(ConsoleMode Mode, TimeSpan Interval)
{
    public const double MinIntervalSeconds = 0.5;
    public const double MaxIntervalSeconds = 5;

    public const string Usage = """
        Usage: DiskPulse.Console [--once | --diag] [--interval <seconds>]

          (no option)        Live table of every physical disk, refreshed until Ctrl+C.
          --once             Print one sample and exit.
          --diag             Print OS language, resolved counter names, disks and one sample.
                             Exits with code 1 if no disk reports data.
          --interval <s>     Refresh interval in seconds, 0.5 to 5 (default 1).
          --help             Show this help.
        """;

    /// <summary>Returns false with a null error when help was requested.</summary>
    public static bool TryParse(string[] args, [NotNullWhen(true)] out ConsoleOptions? options, out string? error)
    {
        options = null;
        error = null;
        var mode = ConsoleMode.Live;
        double seconds = 1;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--once":
                    mode = ConsoleMode.Once;
                    break;
                case "--diag":
                    mode = ConsoleMode.Diagnostics;
                    break;
                case "--interval":
                    // Invariant culture so "0.5" works the same on pt-BR, where the decimal separator is ",".
                    if (i + 1 >= args.Length ||
                        !double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
                        seconds < MinIntervalSeconds || seconds > MaxIntervalSeconds)
                    {
                        error = $"--interval must be a number of seconds between {MinIntervalSeconds} and {MaxIntervalSeconds}.";
                        return false;
                    }
                    break;
                case "--help" or "-h" or "/?":
                    return false;
                default:
                    error = $"Unknown option: {args[i]}";
                    return false;
            }
        }

        options = new ConsoleOptions(mode, TimeSpan.FromSeconds(seconds));
        return true;
    }
}

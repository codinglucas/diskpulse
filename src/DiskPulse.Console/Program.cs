using DiskPulse.ConsoleApp;
using DiskPulse.Core;

if (!ConsoleOptions.TryParse(args, out var options, out string? error))
{
    if (error is not null)
        Console.Error.WriteLine(error);
    Console.Error.WriteLine(ConsoleOptions.Usage);
    return error is null ? 0 : 2;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    return options.Mode switch
    {
        ConsoleMode.Diagnostics => Diagnostics.Run(options.Interval),
        ConsoleMode.Once => await LiveView.RunOnceAsync(options.Interval),
        _ => await LiveView.RunAsync(options.Interval),
    };
}
catch (DiskMetricsException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

# DiskPulse

An open-source Windows overlay that shows real-time health metrics for every physical disk:
response time, active time, queue length and throughput. It is built to make a struggling
drive obvious at a glance.

> **Status:** early development. Only the metrics engine and a test console exist so far.
> The overlay is coming in the next milestones.

Repository: <https://github.com/codinglucas/diskpulse>

## Requirements

- Windows 10 or 11, x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Build and run the test console

```
dotnet build
dotnet run --project src/DiskPulse.Console
```

Options:

| Option | What it does |
|---|---|
| *(none)* | Live table for every disk, refreshed every second until Ctrl+C |
| `--once` | Print one sample and exit |
| `--diag` | Print the OS language, resolved counter names, disks and one sample |
| `--interval <s>` | Refresh interval, 0.5 to 5 seconds |

## Tests

```
dotnet test
```

The unit tests cover the platform-independent engine (`DiskPulse.Core`), so they also run
on macOS and Linux. Reading live counters needs Windows. On a non-English Windows install,
follow [docs/localization-check.md](docs/localization-check.md).

## Project layout

| Project | Purpose |
|---|---|
| `src/DiskPulse.Core` | Models, unit conversions and parsing. Cross-platform and unit-tested. |
| `src/DiskPulse.Windows` | PDH counter reader and WMI disk-info provider |
| `src/DiskPulse.Console` | Console front end for testing the engine |
| `tests/DiskPulse.Core.Tests` | xUnit tests |

## License

[MIT](LICENSE) © 2026 Lucas Rocha Avelino

# DiskPulse

An open-source Windows overlay that shows real-time health metrics for every physical disk:
response time, active time, queue length and throughput. It is built to make a struggling
drive obvious at a glance.

> **Status:** early development. A basic overlay with color coding works. Click-through,
> the tray icon, the hotkey and settings come in the next milestone.

Repository: <https://github.com/codinglucas/diskpulse>

## Requirements

- Windows 10 or 11, x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Run the overlay

```
dotnet run --project src/DiskPulse.App
```

A small window shows one row per physical disk, in the top-left corner and always on top.
Drag it with the left mouse button. Hover over a disk to see what its warnings mean.
Right-click it and choose **Exit DiskPulse** to close it.

| Dot | Meaning (defaults) |
|---|---|
| Green | No problems detected |
| Yellow | For at least 5 s: response time over 50 ms, or more than 2 requests waiting with response time over 20 ms |
| Red | Response time over 200 ms for at least 5 s, or **busy but not moving data**: at least 90% active while transferring under 1 MB/s for 10 s, a classic sign of a failing drive or bad cable |

The overlay follows the Windows display language. English and Brazilian Portuguese are included.

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
| `src/DiskPulse.Core` | Models, unit conversions, parsing and the health rules. Cross-platform and unit-tested. |
| `src/DiskPulse.App` | WPF overlay |
| `src/DiskPulse.Windows` | PDH counter reader and WMI disk-info provider |
| `src/DiskPulse.Console` | Console front end for testing the engine |
| `tests/DiskPulse.Core.Tests` | xUnit tests |

## License

[MIT](LICENSE) © 2026 Lucas Rocha Avelino

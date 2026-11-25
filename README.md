# DiskPulse

[![CI](https://github.com/codinglucas/diskpulse/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/codinglucas/diskpulse/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

An open-source Windows overlay that shows real-time health metrics for every physical disk:
response time, active time, queue length and throughput. It is built to make a struggling
drive obvious at a glance.

> **Status:** early development. The overlay, color coding, tray icon, hotkey and settings
> work. Sparkline graphs and CSV logging come next.

Repository: <https://github.com/codinglucas/diskpulse>

## Requirements

- Windows 10 or 11, x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Run the overlay

```
dotnet run --project src/DiskPulse.App
```

A small window shows one row per physical disk and stays on top of other windows. Hover
over a disk to see what its warnings mean.

| To… | Do this |
|---|---|
| Move the overlay | Drag it with the left mouse button. It remembers where you left it on each monitor. |
| Show or hide it | Press **Ctrl+Shift+D**, or click the DiskPulse icon in the notification area. |
| Stop it blocking clicks | Turn on **Click-through** from the right-click menu or the tray icon. Clicks then go to the window underneath; use the tray icon to turn it off again. |
| See the last minute | Turn on **Expanded view** from either menu. Each disk gets a 60-second graph of its response time; the dashed line is the yellow-warning limit. |
| Save what just happened | Choose **Export last 10 minutes…** from either menu or the settings window. DiskPulse keeps the last 10 minutes in memory and saves them as one CSV file, wherever you choose. |
| Change settings | Right-click the overlay or the tray icon and choose **Settings…** |
| Exit | **Exit DiskPulse** in either menu. |

The tray icon has the same color as the worst disk, and its tooltip names the problem.

| Dot | Meaning (defaults) |
|---|---|
| Green | Performance looks normal. DiskPulse doesn't check SMART health data yet. |
| Yellow | For at least 5 s: response time over 50 ms, or more than 2 requests waiting with response time over 20 ms |
| Red | Response time over 200 ms for at least 5 s, or **busy but not moving data**: at least 90% active while transferring under 1 MB/s for 10 s, a classic sign of a failing drive or bad cable |

### Settings

The settings window lets you change:

- **General:** language (English, Português (Brasil), or the same as Windows), the show/hide shortcut, and starting with Windows.
- **Overlay:** click-through, opacity, font size, refresh rate (0.5 to 5 s), and which metrics to show.
- **Overlay:** also the expanded view with the 60-second graph.
- **Warnings:** every yellow and red limit above, and how long each must last.
- **CSV log:** off by default. When on, every reading is appended to one CSV file per disk
  (`DiskPulse_disk0_20261124_153000.csv`) in the folder you choose, by default
  `%LOCALAPPDATA%\DiskPulse\Logs`. A new file starts when one reaches the size limit
  (10 MB by default) and the oldest are deleted beyond the number you keep (10 per disk).
  DiskPulse only ever deletes files with that exact name pattern. Put the log on a disk
  you are *not* troubleshooting, so the log itself doesn't add load to it.

Settings are saved in `%APPDATA%\DiskPulse\settings.json`. If that file gets damaged,
DiskPulse starts with defaults and keeps the damaged copy as `settings.invalid.json`.

### CSV columns

The log and the export use the same columns. Numbers always use `.` as the decimal
separator, whatever your Windows region. If Excel puts everything in one column, use
**Data → From Text/CSV** and choose comma as the delimiter.

| Column | Meaning |
|---|---|
| `timestamp` | Local time with UTC offset, e.g. `2026-11-24T15:30:00.000-03:00` |
| `disk`, `drive_letters`, `model`, `type` | Which disk |
| `health`, `warnings` | `Ok`, `Warning` or `Critical`, and the active warnings separated by `;` |
| `response_ms`, `read_response_ms`, `write_response_ms` | Average time per request |
| `active_percent` | 100 − idle time |
| `queue_length` | Requests waiting at that moment |
| `read_mbps`, `write_mbps` | MB/s (1 MB = 1,048,576 bytes, like Task Manager) |

### Games

The overlay shows over games in **windowed** or **borderless windowed** mode. Games in
**exclusive fullscreen** draw directly to the screen and can hide it. If you can't see the
overlay in a game, switch the game to borderless windowed in its graphics settings.

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

The unit tests cover the platform-independent engine and settings (`DiskPulse.Core`), plus a
check that every translation has the same strings and placeholders as English. They also run
on macOS and Linux. Reading live counters needs Windows.

Every pull request and every push to `main` runs [CI](.github/workflows/ci.yml) on Windows with
.NET 8. It builds the solution, runs the tests, and runs `DiskPulse.Console --diag` against the
runner's real disks. CI uses English Windows, so to check another language, follow
[docs/localization-check.md](docs/localization-check.md).

## Project layout

| Project | Purpose |
|---|---|
| `src/DiskPulse.Core` | Models, unit conversions, parsing, health rules and settings. Cross-platform and unit-tested. |
| `src/DiskPulse.App` | WPF overlay, tray icon, hotkey and settings window |
| `src/DiskPulse.Windows` | PDH counter reader and WMI disk-info provider |
| `src/DiskPulse.Console` | Console front end for testing the engine |
| `tests/DiskPulse.Core.Tests` | xUnit tests |

## License

[MIT](LICENSE) © 2026 Lucas Rocha Avelino

# Temperature Intensity Monitor

A tiny, transparent, always-on-top overlay for Windows that shows **CPU & GPU
temperature, load and power draw** — no bloat, no background services, no ads.

```
CPU  62°  34%  125W
GPU  48°  12%   44W
```

## Why

Most hardware monitoring suites ship with updaters, RGB integrations and
half a gigabyte of "gaming" UI. This is the opposite: one small window,
one dependency, ~1% of a single core while polling once per second.

## Features

- Transparent, rounded, always-on-top overlay (drag it anywhere; position is remembered)
- CPU: package temperature, total load %, package power (W)
- GPU: core temperature, load %, board power (W) — NVIDIA, AMD and Intel
- Temperature color coding: green < 60°, yellow < 80°, red ≥ 80°
- Optional **click-through** mode (overlay ignores the mouse — toggle from the tray icon)
- Tray icon with live tooltip and exit
- Single instance, settings stored in `%AppData%\TempMonitor\settings.json`

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Administrator rights** — reading CPU package temperature requires MSR
  access through LibreHardwareMonitor's kernel driver. The app requests
  elevation on start; without it the CPU rows would be empty.
- **[PawnIO](https://pawnio.eu/)** (`winget install namazso.PawnIO`) — on
  Windows 11 with Memory Integrity (HVCI) enabled, the classic WinRing0
  driver is blocked and CPU temperature/power show `--`. PawnIO is a
  Microsoft-signed, HVCI-compatible replacement that LibreHardwareMonitor
  picks up automatically.

## Build & run

```powershell
dotnet publish src/TempMonitor -c Release -o publish
./publish/TempMonitor.exe
```

## Start with Windows (optional)

Because the app needs elevation, use Task Scheduler rather than the Startup folder:

```powershell
$exe = (Resolve-Path .\publish\TempMonitor.exe).Path
Register-ScheduledTask -TaskName "TempMonitor" `
  -Action (New-ScheduledTaskAction -Execute $exe) `
  -Trigger (New-ScheduledTaskTrigger -AtLogOn) `
  -RunLevel Highest
```

## Credits

Sensor access via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0).

## License

MIT

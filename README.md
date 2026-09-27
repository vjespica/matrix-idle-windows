# Matrix Idle for Windows

A small, source-built digital rain animation that appears after you stop using your Windows PC. It covers every connected display and disappears when you move the mouse or press a key.

[Leer en español](README.es.md)

## What it does

- Waits 60 seconds by default; accepts 5–3600 seconds with `--idle-seconds`.
- Draws a full-screen animation on every connected monitor.
- Runs in the signed-in user's session without administrator access.
- The installer adds a sign-in entry and a scheduled task that restarts the app if it stops. Only one instance runs per session.

This is a full-screen idle app, **not** a native Windows `.scr` module. It does not lock the computer or request a password. Configure Windows sign-in and lock settings separately if you need that protection. If another screen saver is enabled, its activation may overlap this app.

## Requirements

- Windows 11 (tested on Windows 11 Pro, build 26200); Windows 10 has not been tested.
- .NET Framework 4.x with its C# compiler (`csc.exe`).
- PowerShell and the Windows Scheduled Tasks module for installation.

## Build and try it

Open PowerShell in this repository:

```powershell
.\scripts\build.ps1
.\dist\MatrixIdle.exe --idle-seconds 60
```

The first command writes `dist\MatrixIdle.exe`. Leave the second command running. After the selected idle time, the animation appears. Mouse or keyboard input hides the animation while the background process continues watching for the next idle period. To stop it completely:

```powershell
$builtExe = (Resolve-Path .\dist\MatrixIdle.exe).Path
Get-CimInstance Win32_Process -Filter "Name='MatrixIdle.exe'" |
    Where-Object { $_.ExecutablePath -eq $builtExe } |
    ForEach-Object { Stop-Process -Id $_.ProcessId }
```

## Install for the current Windows user

```powershell
.\scripts\install.ps1 -IdleSeconds 60
```

The script builds the executable, installs it under `%LOCALAPPDATA%\MatrixIdle`, starts it, adds an `HKCU\...\Run` entry for sign-in, and registers `MatrixIdleWatchdog` to recover it within about one minute if it stops. The task also starts at sign-in. It does not need administrator access. If another Matrix Idle installation already owns these startup entries, the script stops and explains how to replace it explicitly.

To remove the installation:

```powershell
.\scripts\uninstall.ps1
```

## Troubleshooting

```powershell
Get-Process MatrixIdle -ErrorAction SilentlyContinue
Get-ScheduledTaskInfo -TaskName MatrixIdleWatchdog
Get-Content "$env:LOCALAPPDATA\MatrixIdle\MatrixIdle.log" -Tail 20
```

The log records starts, the initial idle reading, and each time the animation opens. The repository excludes generated executables and logs; build locally from the source in [`src/MatrixIdle.cs`](src/MatrixIdle.cs).

## License

See [`LICENSE`](LICENSE).

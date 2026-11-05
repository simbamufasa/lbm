# Charge Manager — experimental Windows tray app

This is an **early, untested Windows prototype**, based on the MIT-licensed
[LBM by Marek Wesołowski](https://github.com/wesmar/lbm).
The original LBM code and LICENSE.md remain unchanged.

## Features

- Single-instance WinForms tray process (.NET 10, Windows 11).
- Daily: 60/80, Gaming/AC: 75/80, Travel: disable thresholds, Custom: 0..99.
- Read current thresholds through LBM's CLI; battery and external power via Windows.
- Threshold writes only after explicit user input; **no background charge cycling**.
- Read-back verification after writes, including delayed UAC relaunches.
- Local settings saved to %LOCALAPPDATA%\ChargeManager\settings.json.
- Closing the window minimizes to the tray; Quit from the tray context menu.

## Build and run (Windows)

Requires .NET 10 SDK, MSVC/Windows SDK for upstream LBM, and the Lenovo PM driver.

```powershell
.\build.ps1
dotnet publish .\app\ChargeManager\ChargeManager.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\dist\ChargeManager
Copy-Item .\bin\lbm.exe .\dist\ChargeManager\lbm.exe -Force
.\dist\ChargeManager\ChargeManager.exe
```

The app is framework-dependent and requires the .NET 10 Windows Desktop Runtime.
Do not run the tray as Administrator; upstream LBM is responsible for requesting
UAC for privileged changes. Test read-only status before any write.

## Known limitations / safety

- **Not yet compiled or hardware-tested through the tray.** Verify on a Windows machine.
- CLI output capture of LBM's GUI-subsystem binary needs validation. Some LBM output
  or UAC-relayed status may not be available to a redirected process.
- The app checks configuration, not electrical battery isolation or live charge current.
- Charge limits do not force the laptop to discharge to 80%.
- In Travel mode, LBM's --disable releases the charge threshold (not a 100% set).
- There is no automatic game detection or startup registration in this milestone.
- The upstream driver's firmware and hardware compatibility restrictions remain.
- If changes fail, inspect using `bin\lbm.exe --status`, Lenovo Vantage, and the
  original terminal commands rather than assuming the app has succeeded.

## Next steps

1. Verify builds and CLI/UAC lifecycle on Windows.
2. Test profiles against Lenovo Vantage and actual battery charge state.
3. Improve tray icon, controls, accessibility, DPI support and packaging.
4. Add opt-in startup and game detection only after hardware validation.

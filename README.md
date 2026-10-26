# FSE Tool

FSE Tool enters and exits XBOX mode directly without sending keyboard shortcuts. Useful in HTPCs or as pre-launch command for Sunshine/Apollo when setting up game streaming

It also provides a bypass for the "Restart for better performance" confirmation dialog.

## Prerequisites

Enable Xbox mode in Windows Settings > Gaming > XBOX mode, and select a supported home app such as **XBOX** under **Choose home app**.

## Installation

Download the latest release from the repository and place `fse-tool.exe` in a folder of your choice. Optionally, add the folder to your system PATH to run `fse-tool` from any location.

## Commands

```text
fse-tool start
fse-tool exit
fse-tool toggle
fse-tool status

fse-tool install
fse-tool uninstall
```

- `start` enters Xbox mode; it does nothing if already active.
- `exit` returns to the desktop; it does nothing if already on the desktop.
- `toggle` switches between Xbox mode and the desktop.
- `status` prints exactly `Desktop` or `Xbox`.
- `install` enables the entry confirmation bypass for the current user.
- `uninstall` removes the bypass value and restores default confirmation behavior.

## Manual registry setup

If you don't want to use `fse-tool install`, you can set the registry value in your terminal manually:

```powershell
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults" /v EnterGamingPostureConfirmation_Config /t REG_DWORD /d 2 /f
```

To remove the bypass manually, equivalent to `fse-tool uninstall`:

```powershell
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults" /v EnterGamingPostureConfirmation_Config /f
```

The value is absent by default. In Registry Editor, the same setup is a **DWORD (32-bit) Value** named `EnterGamingPostureConfirmation_Config`, set to `2`, under:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults
```

## Compatibility

Verified on Windows 26H2. This tool uses an undocumented internal Windows controller interface, which may change in future Windows updates. The matching source is included as fse-tool.cs.

## Building and releasing

Run `just build` to compile the exe.

```powershell
just build

OR

@Add-Type -Path ./fse-tool.cs -OutputAssembly ./fse-tool.exe -OutputType ConsoleApplication -ErrorAction Stop
```

To publish a version:

```text
just bump 0.2.0-beta.1
# or, for a stable release:
just bump 0.2.0
```

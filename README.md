# FSE Tool

FSE Tool enters and exits XBOX mode directly without sending keyboard shortcuts. Useful in HTPCs or as pre-launch command for Sunshine/Apollo when setting up game streaming

It also provides a bypass for the "Restart for better performance" confirmation dialog.

## Prerequisites

Enable Xbox mode in Windows Settings > Gaming > XBOX mode, and select a supported home app such as **XBOX** under **Choose home app**.

Use tools like [XboxFullScreenExperienceTool](https://github.com/8bit2qubit/XboxFullscreenExperienceTool) to enable Xbox fullscreen experience on your system.

## Installation

PowerShell:

```powershell
irm https://raw.githubusercontent.com/lmrisdal/fse-tool/main/install.ps1 | iex
```

Run the same command again to update or uninstall.

You can also download `fse-tool.exe` from the [latest release](https://github.com/lmrisdal/fse-tool/releases/latest) and place it in a folder of your choice.

## Commands

```text
fse-tool start
fse-tool start --app <target> [args...]
fse-tool stop
fse-tool toggle
fse-tool status

fse-tool bypass enable
fse-tool bypass disable
fse-tool bypass status

fse-tool home install
fse-tool home uninstall
fse-tool home status
fse-tool home set <target> [args...]

fse-tool version
fse-tool help
```

- `start` enters Xbox mode; it does nothing if already active.
- `start --app` enters Xbox mode and opens `<target>` (a URI, `shell:AppsFolder\<AUMID>`, or an exe path). If Xbox mode is already active, it just opens the target. See [Sunshine / Apollo](#sunshine--apollo).
- `stop` returns to the desktop; it does nothing if already on the desktop.
- `toggle` switches between Xbox mode and the desktop.
- `status` prints exactly `Desktop` or `Xbox`.
- `bypass enable` enables the entry confirmation bypass for the current user.
- `bypass disable` removes the bypass value and restores default confirmation behavior.
- `bypass status` prints exactly `Enabled` or `Disabled`.
- `home install` registers fse-tool as an Xbox mode home app. Run it again after updating fse-tool.
- `home uninstall` removes the home app. It refuses while fse-tool is the selected home app.
- `home status` prints exactly `NotInstalled`, `Installed`, or `Selected` (installed and chosen as the home app).
- `home set` changes what the home app opens. The default is `steam://open/bigpicture`.
- `version` (also `--version` and `-v`) prints the version the exe was built from, such as `0.2.0` for a release.
- `help` (also `--help` and `-h`) lists all commands. Running `fse-tool` with no command does the same.

## Home app

fse-tool can be the app Xbox mode opens when it starts, in place of the Xbox app. By default it opens Steam Big Picture.

1. Turn on Developer Mode in Settings > System > Advanced. Windows only accepts the home-app capability from unsigned packages in Developer Mode.
2. Run `fse-tool home install`. This writes a small package to `%LOCALAPPDATA%\fse-tool\home` and registers it for the current user.
3. In Settings > Gaming > XBOX mode > **Choose home app**, select **FSE Tool**.

To open something other than Steam Big Picture:

```powershell
fse-tool home set "%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe"
fse-tool home set steam://open/bigpicture
```

The home app writes its last launch to `%LOCALAPPDATA%\fse-tool\home.log`.

> [!WARNING]
> Choose a different home app before running `fse-tool home uninstall`. Removing the selected home app can break Task View on some Windows builds.

## Sunshine / Apollo

Add one application per launcher, with `fse-tool start --app` as the prep command and `fse-tool stop` as the undo command:

| Application | Do command | Undo command |
| --- | --- | --- |
| Steam | `fse-tool start --app steam://open/bigpicture` | `fse-tool stop` |
| Xbox | `fse-tool start --app shell:AppsFolder\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App` | `fse-tool stop` |
| Playnite | `fse-tool start --app "%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe"` | `fse-tool stop` |

If fse-tool is the selected home app, Xbox mode opens the requested target in place of the default. Otherwise fse-tool opens the target after Xbox mode is active, on top of whichever home app is selected.

## Manual registry setup

If you don't want to use `fse-tool bypass enable`, you can set the registry value in your terminal manually:

```powershell
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults" /v EnterGamingPostureConfirmation_Config /t REG_DWORD /d 2 /f
```

To remove the bypass manually, equivalent to `fse-tool bypass disable`:

```powershell
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults" /v EnterGamingPostureConfirmation_Config /f
```

The value is absent by default. In Registry Editor, the same setup is a **DWORD (32-bit) Value** named `EnterGamingPostureConfirmation_Config`, set to `2`, under:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults
```

## Compatibility

Verified on Windows 26H2. This tool uses an undocumented internal Windows controller interface, which may change in future Windows updates. The matching source is included in `src/`.

## Building and releasing

Run `just build` (or `./build.ps1`) to compile `build/fse-tool.exe`. The version comes from `git describe`, such as `0.1.1-6-gd8449d1-dirty` for an untagged build. Release builds use the pushed tag.

```powershell
just build

OR

./build.ps1
./build.ps1 -Version v0.2.0 -Output ./fse-tool.exe
```

Compiling `src/*.cs` directly with `Add-Type` also works, but `fse-tool version` then reports `dev`.

To publish a version:

```text
just bump 0.2.0-beta.1
# or, for a stable release:
just bump 0.2.0
```

## License

fse-tool is licensed under the GNU General Public License v3.0 (GPL-3.0-only). See [LICENSE](LICENSE).

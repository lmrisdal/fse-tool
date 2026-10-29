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

Run `fse-tool update` to update to the latest release.

To uninstall:

```powershell
irm https://raw.githubusercontent.com/lmrisdal/fse-tool/main/uninstall.ps1 | iex
```

You can also download `fse-tool.exe` from the [latest release](https://github.com/lmrisdal/fse-tool/releases/latest) and place it in a folder of your choice.

## Commands

### General

| Command   | Description                                                                                                       |
| --------- | ----------------------------------------------------------------------------------------------------------------- |
| `help`    | Lists all commands. Also `--help`, `-h`, or no command at all.                                                    |
| `update`  | Downloads the latest release over the running exe if it is newer, and refreshes the home app if one is installed. |
| `version` | Prints the version the exe was built from, such as `0.2.0`. Also `--version` and `-v`.                            |

### Xbox mode

| Command                          | Description                                                                                                                                                                                     |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `start`                          | Enters Xbox mode. Does nothing if already active.                                                                                                                                               |
| `start --app <target> [args...]` | Enters Xbox mode and opens `<target>`: a URI, `shell:AppsFolder\<AUMID>`, or an exe path. If Xbox mode is already active, it just opens the target. See [Sunshine / Apollo](#sunshine--apollo). |
| `stop`                           | Returns to the desktop. Does nothing if already on the desktop.                                                                                                                                 |
| `toggle`                         | Switches between Xbox mode and the desktop.                                                                                                                                                     |
| `status`                         | Prints `Desktop` or `Xbox`.                                                                                                                                                                     |

### Confirmation bypass

| Command          | Description                                                             |
| ---------------- | ----------------------------------------------------------------------- |
| `bypass enable`  | Skips the "Restart for better performance" dialog for the current user. |
| `bypass disable` | Removes the bypass and restores the dialog.                             |
| `bypass status`  | Prints `Enabled` or `Disabled`.                                         |

### Home app

| Command                           | Description                                                                                                                         |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `home install`                    | Registers fse-tool as an Xbox mode home app. `update` reruns it for you; run it again yourself after updating fse-tool another way. |
| `home uninstall`                  | Removes the home app. Refuses while fse-tool is the selected home app.                                                              |
| `home status`                     | Prints `NotInstalled`, `Installed`, or `Selected` (installed and chosen as the home app).                                           |
| `home set`                        | Lists the home apps under **Choose home app** and marks the selected one.                                                           |
| `home set <name or AUMID>`        | Selects a home app, for example `home set XBOX`, `home set "FSE Tool"`, or `home set None`.                                         |
| `home default`                    | Prints what fse-tool opens when Xbox mode starts. The default is `steam://open/bigpicture`.                                         |
| `home default <target> [args...]` | Changes what fse-tool opens when Xbox mode starts.                                                                                  |

## Home app

fse-tool can be the app Xbox mode opens when it starts, in place of the Xbox app. By default it opens Steam Big Picture.

1. Turn on Developer Mode in Settings > System > Advanced. Windows only accepts the home-app capability from unsigned packages in Developer Mode.
2. Run `fse-tool home install`. This writes a small package to `%LOCALAPPDATA%\fse-tool\home` and registers it for the current user.
3. Select **FSE Tool** in Settings > Gaming > XBOX mode > **Choose home app**.

To open something other than Steam Big Picture:

```powershell
fse-tool home default "%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe"
fse-tool home default steam://open/bigpicture
```

The home app writes its last launch to `%LOCALAPPDATA%\fse-tool\home.log`.

> [!WARNING]
> Choose a different home app before running `fse-tool home uninstall`. Removing the selected home app can break Task View on some Windows builds.

## Sunshine / Apollo

Add one application per launcher, with `fse-tool start --app` as the prep command and `fse-tool stop` as the undo command:

| Application | Do command                                                                                   | Undo command    |
| ----------- | -------------------------------------------------------------------------------------------- | --------------- |
| Steam       | `fse-tool start --app steam://open/bigpicture`                                               | `fse-tool stop` |
| Xbox        | `fse-tool start --app shell:AppsFolder\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App` | `fse-tool stop` |
| Playnite    | `fse-tool start --app "%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe"`                  | `fse-tool stop` |

If fse-tool is the selected home app, Xbox mode opens the requested target in place of the default. Otherwise fse-tool opens the target after Xbox mode is active, on top of whichever home app is selected.

## Bypass confirmation dialog

The bypass is a single registry value for the current user. `fse-tool bypass enable` creates a **DWORD (32-bit) Value** named `EnterGamingPostureConfirmation_Config`, set to `2`, under:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults
```

Windows reads this value as a saved answer to the "Restart for better performance" confirmation dialog, so it skips the dialog when entering Xbox mode. The value is absent by default, and `fse-tool bypass disable` deletes it, which brings the dialog back.

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

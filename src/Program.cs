using System;

namespace XboxFseDirect {
    public static class Program {
        const string Help = @"Usage: fse-tool <command>

XBOX mode:
  start                          Enter XBOX mode.
  start --app <target> [args]    Enter XBOX mode and open <target>: a URI, shell:AppsFolder\<AUMID>, or an exe path. 
                                 The home app must be set to FSE Tool (see 'home install').
  stop                           Return to the desktop.
  toggle                         Switch between XBOX mode and the desktop.
  status                         Print Xbox or Desktop.

Confirmation dialog bypass:
  bypass enable                  Skip the ""Restart for better performance"" dialog.
  bypass disable                 Restore the dialog.
  bypass status                  Print Enabled or Disabled.

Home app:
  home install                   Register fse-tool as an XBOX mode home app (requires Developer Mode).
  home uninstall                 Remove the home app. Choose another home app first.
  home status                    Print NotInstalled, Installed, or Selected.
  home set                       List the home apps Windows offers, marking the selected one.
  home set <name or AUMID>       Change the XBOX mode home app, e.g. 'home set ""FSE Tool""' or 'home set None'.
  home default                   Print what FSE Tool opens when XBOX mode starts.
  home default <target> [args]   Set what FSE Tool opens (default: " + HomeApp.DefaultTarget + @").

  update                         Update fse-tool to the latest release.
  version, --version, -v         Print the fse-tool version.
  help, --help, -h               Show this help.";

        static void StartWithApp(string target, string arguments) {
            Bypass.WarnIfDisabled();
            if (Controller.IsActive()) {
                HomeApp.Launch(target, arguments);
                System.Console.WriteLine("XBOX mode is already active. Launched " + target + ".");
            } else if (HomeApp.IsSelected()) {
                // The home app starts with XBOX mode and opens this target instead of its default.
                HomeApp.SetPending(target, arguments);
                try {
                    System.Console.WriteLine(Controller.SetActive(true, 20));
                } catch {
                    HomeApp.ClearPending();
                    throw;
                }
            } else {
                System.Console.WriteLine(Controller.SetActive(true, 20));
                HomeApp.Launch(target, arguments);
                System.Console.WriteLine("Launched " + target + ".");
            }
        }

        public static int Main(string[] args) {
            if (HomeApp.IsPackaged()) return HomeApp.RunAsHome(args);
            Updater.DeleteOldExe();
            // Internal: the elevated half of 'update' for installs in Program Files.
            if (args.Length == 4 && args[0] == "update" && args[1] == "--elevated") return Updater.RunElevated(args[2], args[3]);
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "help" || args[0] == "--help" || args[0] == "-h"))) {
                System.Console.WriteLine("fse-tool " + BuildInfo.Version + Environment.NewLine + Environment.NewLine + Help);
                return 0;
            }
            if (args.Length == 1 && (args[0] == "version" || args[0] == "--version" || args[0] == "-v")) {
                System.Console.WriteLine(BuildInfo.Version);
                return 0;
            }
            bool isBypass = args.Length == 2 && args[0] == "bypass" && (args[1] == "enable" || args[1] == "disable" || args[1] == "status");
            bool isHome = args.Length >= 2 && args[0] == "home" && (args.Length == 2
                ? args[1] == "install" || args[1] == "uninstall" || args[1] == "status" || args[1] == "set" || args[1] == "default"
                : args[1] == "set" || args[1] == "default");
            bool isStartWithApp = args.Length >= 3 && args[0] == "start" && args[1] == "--app";
            if (!isBypass && !isHome && !isStartWithApp && (args.Length != 1 || (args[0] != "start" && args[0] != "stop" && args[0] != "status" && args[0] != "toggle" && args[0] != "update"))) {
                System.Console.Error.WriteLine(Help);
                return 2;
            }
            try {
                if (isBypass && args[1] == "status") System.Console.WriteLine(Bypass.IsEnabled() ? "Enabled" : "Disabled");
                else if (isBypass) Bypass.Configure(args[1] == "enable");
                else if (isHome && args[1] == "install") HomeApp.Install();
                else if (isHome && args[1] == "uninstall") HomeApp.Uninstall();
                else if (isHome && args[1] == "status") System.Console.WriteLine(HomeApp.Status());
                // Names may contain spaces, so 'home set FSE Tool' works unquoted too.
                else if (isHome && args[1] == "set") HomeApp.SetSelected(args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : null);
                else if (isHome && args.Length == 2) HomeApp.ShowDefault();
                else if (isHome) HomeApp.SetDefault(args[2], HomeApp.JoinArguments(args, 3));
                else if (isStartWithApp) StartWithApp(args[2], HomeApp.JoinArguments(args, 3));
                else if (args[0] == "update") Updater.Update();
                else if (args[0] == "status") System.Console.WriteLine(Controller.IsActive() ? "Xbox" : "Desktop");
                else {
                    Bypass.WarnIfDisabled();
                    System.Console.WriteLine(Controller.SetActive(args[0] == "toggle" ? !Controller.IsActive() : args[0] == "start", 20));
                }
                return 0;
            } catch (System.Exception error) {
                // COM errors from twinui append internal debug lines; keep only the first.
                string message = error.Message.Split(new[] { '\r', '\n' }, 2)[0].Trim();
                System.Console.Error.WriteLine("fse-tool: " + message + " (0x" + error.HResult.ToString("X8") + ")");
                return 1;
            }
        }
    }
}

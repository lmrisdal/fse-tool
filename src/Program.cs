using System;

namespace XboxFseDirect {
    public static class Program {
        const string Usage = "Usage: fse-tool start [--app <target> [args...]] | stop | status | toggle | bypass enable | bypass disable | bypass status | home install | home uninstall | home status | home set <target> [args...]";

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
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))) {
                System.Console.WriteLine(Usage);
                return 0;
            }
            bool isBypass = args.Length == 2 && args[0] == "bypass" && (args[1] == "enable" || args[1] == "disable" || args[1] == "status");
            bool isHome = args.Length >= 2 && args[0] == "home" && (args.Length == 2
                ? args[1] == "install" || args[1] == "uninstall" || args[1] == "status"
                : args[1] == "set");
            bool isStartWithApp = args.Length >= 3 && args[0] == "start" && args[1] == "--app";
            if (!isBypass && !isHome && !isStartWithApp && (args.Length != 1 || (args[0] != "start" && args[0] != "stop" && args[0] != "status" && args[0] != "toggle"))) {
                System.Console.Error.WriteLine(Usage);
                return 2;
            }
            try {
                if (isBypass && args[1] == "status") System.Console.WriteLine(Bypass.IsEnabled() ? "Enabled" : "Disabled");
                else if (isBypass) Bypass.Configure(args[1] == "enable");
                else if (isHome && args[1] == "install") HomeApp.Install();
                else if (isHome && args[1] == "uninstall") HomeApp.Uninstall();
                else if (isHome && args[1] == "status") System.Console.WriteLine(HomeApp.Status());
                else if (isHome) HomeApp.SetDefault(args[2], HomeApp.JoinArguments(args, 3));
                else if (isStartWithApp) StartWithApp(args[2], HomeApp.JoinArguments(args, 3));
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

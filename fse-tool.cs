using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace XboxFseDirect {
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellServiceProvider {
        [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out IntPtr result);
    }

    public static class Controller {
        [DllImport("api-ms-win-gaming-experience-l1-1-0.dll", ExactSpelling = true)]
        static extern int IsGamingFullScreenExperienceActive();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ModeMethod(IntPtr self, int entryPoint, IntPtr context, out IntPtr action);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReadIntMethod(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NoArgMethod(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int QueryInterfaceMethod(IntPtr self, ref Guid iid, out IntPtr result);

        static T Method<T>(IntPtr obj, int slot) where T : class {
            IntPtr address = Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }

        public static bool IsActive() { return IsGamingFullScreenExperienceActive() != 0; }

        public static string SetActive(bool desired, int timeoutSeconds) {
            if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new ArgumentOutOfRangeException("timeoutSeconds");
            if (IsActive() == desired) return desired ? "Xbox mode is already active." : "Desktop mode is already active.";
            object shell = null;
            IntPtr controller = IntPtr.Zero, action = IntPtr.Zero, info = IntPtr.Zero;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239")));
                Guid service = new Guid("946dcf6a-f83a-4b31-8227-a37e42ca8574");
                Guid iid = new Guid("21c3337a-afaf-5470-aa95-90fc8d496d8c");
                Marshal.ThrowExceptionForHR(((IShellServiceProvider)shell).QueryService(ref service, ref iid, out controller));
                if (controller == IntPtr.Zero) throw new InvalidOperationException("Windows returned no gaming controller.");
                // IInspectable slots 0-5 precede EnterGamingPostureAsync (6) and ExitGamingPostureAsync (7).
                // Match the native hotkey handler: entry point 1 and null HSTRING context.
                Marshal.ThrowExceptionForHR(Method<ModeMethod>(controller, desired ? 6 : 7)(controller, 1, IntPtr.Zero, out action));
                if (action == IntPtr.Zero) throw new InvalidOperationException("Windows returned no async action.");
                Guid asyncInfo = new Guid("00000036-0000-0000-C000-000000000046");
                Marshal.ThrowExceptionForHR(Method<QueryInterfaceMethod>(action, 0)(action, ref asyncInfo, out info));
                Stopwatch timer = Stopwatch.StartNew();
                while (true) {
                    int status;
                    Marshal.ThrowExceptionForHR(Method<ReadIntMethod>(info, 7)(info, out status));
                    if (status == 1) {
                        Marshal.ThrowExceptionForHR(Method<NoArgMethod>(action, 8)(action));
                        break;
                    }
                    if (status == 2) throw new OperationCanceledException("Windows canceled the mode change.");
                    if (status == 3) {
                        int error;
                        Marshal.ThrowExceptionForHR(Method<ReadIntMethod>(info, 8)(info, out error));
                        Marshal.ThrowExceptionForHR(error);
                        throw new InvalidOperationException("Windows reported an async error.");
                    }
                    if (status != 0) throw new InvalidOperationException("Unknown async status: " + status);
                    if (timer.Elapsed.TotalSeconds >= timeoutSeconds)
                        throw new TimeoutException("Windows has not completed the mode change. Check for a pending confirmation dialog; the request may still complete.");
                    Thread.Sleep(100);
                }
                // Allow the shell state query to catch up with async completion.
                while (IsActive() != desired && timer.Elapsed.TotalSeconds < timeoutSeconds) Thread.Sleep(100);
                if (IsActive() != desired) throw new InvalidOperationException("The request completed, but Windows did not report the requested mode.");
                return desired ? "Entered Xbox mode (direct controller call; no keys sent)." : "Exited Xbox mode (direct controller call; no keys sent).";
            } finally {
                if (info != IntPtr.Zero) Marshal.Release(info);
                if (action != IntPtr.Zero) Marshal.Release(action);
                if (controller != IntPtr.Zero) Marshal.Release(controller);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }
    }
}

namespace XboxFseDirect {
    public static class Program {
        const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults";
        const string RegistryValue = "EnterGamingPostureConfirmation_Config";
        const string Usage = "Usage: fse-tool start | exit | status | toggle | install | uninstall";

        static bool IsDialogBypassEnabled(Microsoft.Win32.RegistryKey key) {
            object value = key == null ? null : key.GetValue(RegistryValue);
            return value is int && (int)value == 2 && key.GetValueKind(RegistryValue) == Microsoft.Win32.RegistryValueKind.DWord;
        }

        static void ConfigureDialogBypass(bool install) {
            using (var key = install
                ? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryPath)
                : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath, true)) {
                if (install) {
                    if (key == null) throw new InvalidOperationException("Could not open the Xbox mode confirmation settings.");
                    key.SetValue(RegistryValue, 2, Microsoft.Win32.RegistryValueKind.DWord);
                    if (!IsDialogBypassEnabled(key)) throw new InvalidOperationException("Could not verify the installed confirmation bypass.");
                    System.Console.WriteLine("Installed: Xbox mode entry confirmation bypass enabled.");
                } else {
                    if (key != null) {
                        key.DeleteValue(RegistryValue, false);
                        if (Array.IndexOf(key.GetValueNames(), RegistryValue) >= 0)
                            throw new InvalidOperationException("Could not verify removal of the confirmation bypass.");
                    }
                    System.Console.WriteLine("Uninstalled: Xbox mode entry confirmation bypass removed.");
                }
            }
        }

        static void WarnIfDialogBypassIsDisabled() {
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath)) {
                    if (IsDialogBypassEnabled(key)) return;
                }
                System.Console.Error.WriteLine("Notice: EnterGamingPostureConfirmation_Config is not set to DWORD 2. Run 'fse-tool install' to skip the Xbox mode confirmation dialog.");
            } catch (System.Security.SecurityException) {
                System.Console.Error.WriteLine("Notice: Could not read the Xbox mode confirmation setting. Run 'fse-tool install' if a confirmation dialog appears.");
            } catch (System.UnauthorizedAccessException) {
                System.Console.Error.WriteLine("Notice: Could not read the Xbox mode confirmation setting. Run 'fse-tool install' if a confirmation dialog appears.");
            }
        }

        public static int Main(string[] args) {
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))) {
                System.Console.WriteLine(Usage);
                return 0;
            }
            if (args.Length != 1 || (args[0] != "start" && args[0] != "exit" && args[0] != "status" && args[0] != "toggle" && args[0] != "install" && args[0] != "uninstall")) {
                System.Console.Error.WriteLine(Usage);
                return 2;
            }
            try {
                if (args[0] == "install" || args[0] == "uninstall") ConfigureDialogBypass(args[0] == "install");
                else if (args[0] == "status") System.Console.WriteLine(Controller.IsActive() ? "Xbox" : "Desktop");
                else {
                    WarnIfDialogBypassIsDisabled();
                    System.Console.WriteLine(Controller.SetActive(args[0] == "toggle" ? !Controller.IsActive() : args[0] == "start", 20));
                }
                return 0;
            } catch (System.Exception error) {
                System.Console.Error.WriteLine("fse-tool: " + error.Message + " (0x" + error.HResult.ToString("X8") + ")");
                return 1;
            }
        }
    }
}

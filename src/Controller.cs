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

        // Windows refuses to enter with ERROR_BAD_ENVIRONMENT when no home app is selected.
        const int ErrorBadEnvironment = unchecked((int)0x8007000A);
        const string NoHomeAppMessage = "No home app is selected. Select one in Settings > Gaming > XBOX mode > Choose home app.";

        public static bool IsActive() { return IsGamingFullScreenExperienceActive() != 0; }

        public static string SetActive(bool desired, int timeoutSeconds) {
            if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new ArgumentOutOfRangeException("timeoutSeconds");
            if (IsActive() == desired) return desired ? "XBOX mode is already active." : "Desktop mode is already active.";
            if (desired && !HomeApp.IsAnySelected()) throw new COMException(NoHomeAppMessage, ErrorBadEnvironment);
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
                return desired ? "Entered XBOX mode." : "Exited XBOX mode.";
            } catch (COMException error) {
                // Fallback for when the registry check above misses it.
                if (desired && error.HResult == ErrorBadEnvironment) throw new COMException(NoHomeAppMessage, error.HResult);
                throw;
            } finally {
                if (info != IntPtr.Zero) Marshal.Release(info);
                if (action != IntPtr.Zero) Marshal.Release(action);
                if (controller != IntPtr.Zero) Marshal.Release(controller);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }
    }
}

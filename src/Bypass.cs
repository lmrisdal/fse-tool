using System;

namespace XboxFseDirect {
    // Skips the "Restart for better performance" confirmation shown when entering XBOX mode.
    public static class Bypass {
        const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\GamingConfiguration\SystemDialogResults";
        const string RegistryValue = "EnterGamingPostureConfirmation_Config";

        static bool IsEnabled(Microsoft.Win32.RegistryKey key) {
            object value = key == null ? null : key.GetValue(RegistryValue);
            return value is int && (int)value == 2 && key.GetValueKind(RegistryValue) == Microsoft.Win32.RegistryValueKind.DWord;
        }

        public static bool IsEnabled() {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath))
                return IsEnabled(key);
        }

        public static void Configure(bool enable) {
            using (var key = enable
                ? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryPath)
                : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath, true)) {
                if (enable) {
                    if (key == null) throw new InvalidOperationException("Could not open the XBOX mode confirmation settings.");
                    key.SetValue(RegistryValue, 2, Microsoft.Win32.RegistryValueKind.DWord);
                    if (!IsEnabled(key)) throw new InvalidOperationException("Could not verify the enabled confirmation bypass.");
                    System.Console.WriteLine("Enabled: XBOX mode entry confirmation bypass enabled.");
                } else {
                    if (key != null) {
                        key.DeleteValue(RegistryValue, false);
                        if (Array.IndexOf(key.GetValueNames(), RegistryValue) >= 0)
                            throw new InvalidOperationException("Could not verify removal of the confirmation bypass.");
                    }
                    System.Console.WriteLine("Disabled: XBOX mode entry confirmation bypass removed.");
                }
            }
        }

        public static void WarnIfDisabled() {
            try {
                if (IsEnabled()) return;
                System.Console.Error.WriteLine("Warning: XBOX mode confirmation dialog bypass is not enabled. Run 'fse-tool bypass enable' to skip this dialog.");
            } catch (System.Security.SecurityException) {
                System.Console.Error.WriteLine("Warning: Could not read the XBOX mode confirmation setting. Run 'fse-tool bypass enable' if a confirmation dialog appears.");
            } catch (System.UnauthorizedAccessException) {
                System.Console.Error.WriteLine("Warning: Could not read the XBOX mode confirmation setting. Run 'fse-tool bypass enable' if a confirmation dialog appears.");
            }
        }
    }
}

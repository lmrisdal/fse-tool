using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XboxFseDirect {
    // Registers fse-tool as an XBOX mode home app: a loose-layout package (Developer Mode) with the
    // windows.gamingApp extension and gamingHome capability, holding a GUI-subsystem copy of this exe.
    public static class HomeApp {
        const string PackageName = "fse-tool.Home";
        const string Publisher = "CN=fse-tool";
        const string SettingsPath = @"Software\fse-tool";
        const string GamingConfigPath = @"Software\Microsoft\Windows\CurrentVersion\GamingConfiguration";
        public const string DefaultTarget = "steam://open/bigpicture";
        const int PendingLifetimeSeconds = 120;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int GetCurrentPackageFullName(ref int length, System.Text.StringBuilder name);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int GetPackagesByPackageFamily(string familyName, ref int count, IntPtr fullNames, ref int bufferLength, IntPtr buffer);

        const string Manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
  xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10""
  xmlns:uap3=""http://schemas.microsoft.com/appx/manifest/uap/windows10/3""
  xmlns:uap4=""http://schemas.microsoft.com/appx/manifest/uap/windows10/4""
  xmlns:rescap=""http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities""
  xmlns:desktop6=""http://schemas.microsoft.com/appx/manifest/desktop/windows10/6""
  IgnorableNamespaces=""uap uap3 uap4 rescap desktop6"">
  <Identity Name=""" + PackageName + @""" Publisher=""" + Publisher + @""" Version=""1.0.0.0"" ProcessorArchitecture=""neutral"" />
  <Properties>
    <DisplayName>FSE Tool</DisplayName>
    <PublisherDisplayName>fse-tool</PublisherDisplayName>
    <Logo>Assets\Logo.png</Logo>
    <desktop6:RegistryWriteVirtualization>disabled</desktop6:RegistryWriteVirtualization>
    <desktop6:FileSystemWriteVirtualization>disabled</desktop6:FileSystemWriteVirtualization>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.26100.0"" MaxVersionTested=""10.0.26200.0"" />
  </Dependencies>
  <Resources>
    <Resource Language=""en-us"" />
  </Resources>
  <Applications>
    <Application Id=""App"" Executable=""fse-tool.exe"" EntryPoint=""Windows.FullTrustApplication"">
      <uap:VisualElements DisplayName=""FSE Tool"" Description=""Opens a launcher when XBOX mode starts."" BackgroundColor=""transparent""
        Square150x150Logo=""Assets\Logo.png"" Square44x44Logo=""Assets\Logo.png"" />
      <Extensions>
        <uap3:Extension Category=""windows.appExtension"">
          <uap3:AppExtension Name=""windows.gamingApp"" Id=""App"" DisplayName=""FSE Tool"" Description=""Opens a launcher when XBOX mode starts."" PublicFolder=""Public"" />
        </uap3:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name=""runFullTrust"" />
    <rescap:Capability Name=""unvirtualizedResources"" />
    <uap4:CustomCapability Name=""Microsoft.appCategory.gamingHome_8wekyb3d8bbwe"" />
  </Capabilities>
</Package>
";

        const string Sccd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<CustomCapabilityDescriptor xmlns=""http://schemas.microsoft.com/appx/2018/sccd"" xmlns:s=""http://schemas.microsoft.com/appx/2018/sccd"">
  <CustomCapabilities>
    <CustomCapability Name=""Microsoft.appCategory.gamingHome_8wekyb3d8bbwe"" />
  </CustomCapabilities>
  <AuthorizedEntities AllowAny=""true"" />
  <Catalog>FFFF</Catalog>
</CustomCapabilityDescriptor>
";

        const string LogoPng = "iVBORw0KGgoAAAANSUhEUgAAAJYAAACWCAYAAAA8AXHiAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAUvSURBVHhe7duvbxtnHMdxw7HncaJIlQo61sKCgkL/AQWDKxuqBgsKVp8nTyFVUaeCsWlSNSlgICBgYHAgZHQqqdSxkEmBgZ6+Z886f31n36+P/Th5f6VXQX19/Eh563x3TgeDhhPG4XHIwihMwjchCz/gFpv/jEf2M/cddJ4H0wdf2BvELJ7FLN7ESZzhjsri+XA8/DZMQ/SdNBpbJGbxau0NcLdl8TqMw0s76fhmNo4VGSfxcm1BYNXHMA1f+n5Kxw6Mk/i5ZBFgnZ29sjDyHa3M4kxFVGgmi9fHr48f+Z7ysc9LPv7QwefSi3q7GCs5GGji3UpU+dmKuz90lcWbk+nJvWVY+WMFfxDQzptlWHESfy85AGjjYx5VfifIE3X0KL9DPBofPfUvAF2ESfhqYH/4F4Au7AkDjxnQO/vNiIH94V8AuiAsSBAWJAgLEoQFCcKCBGFBgrAgQViQICxIEBYkCAsShAUJwoIEYUGCsCBBWJAgLEgQFiQICxKEBQnCggRhQYKwIEFYkCAsSBAWJAgLEoQFCcKCBGFBgrAgQViQICxIEBYkCAsShAUJwoIEYUGCsCBBWJAgLEgQFiQICxKEBYmDDuvi74tZl3l18WptTe/Jj0/y4+y9Pv37yS+R/529dvrH6ezZz89m90/vr62xq72mhLBK1jUWSVlI28b25NdS7zVFhFWy7vs/3/tDaw9hzRGWW7NLVDaENUdYhfVGP438IY2HsOZuZVhtfwgf/vrgl1qOXW89//X57OHbh8vj7ULdYrT3u/znMj+uaVht95o6wiqoGouq6m6vyCKzu0P/96bvvaaOsBbssULVtFnP63Ovh4CwFjZdX1WdhZroc6+HgLAW7Npp09gZzf+bJvrc6yEgrIJt03Zd0/deU0dYBfaRt23sQv7Fby9qXcwX9b3X1N3KsOpM2WMBi6XJ2MPU4uOHTfrea+oIy9l0d1g1dqbbdgZT7DVlhFWiTVz2EbnpAl+111QRVgU7A9W55vJTFZdyrykirC3sGsousOtO1VP6Xew1JbcyLNWdln1XWOd3tOyu0f/bXe913wirhW1nsLIzzL72ui+E1ZKdvTaNP36fe90Hwuqg6v1t6h67q73uGmEVNP2yuer9beoe23avqSOsAhv7hT37TQf/mrfpS2tbwx/f915TR1gFxbE47O7OR2bPqezvN03Zma/vvaaOsAr6mrLvD6v22mT8mikjrII+puq9q/baZPyaKSOsgq5T9hH4v6q9Nhm/ZsoIq6Dt/ymsc8Fftdcm49dMGWE59j2fPfy0s4+tX/YVjoVkr9n7VH3p7FXttcn4NVN20GEhXYQFCcKCBGFBgrAgQViQICxIEBYkCAsShAUJwoIEYUGCsCBBWJAgLEgQFiQICxKEBQnCggRhQYKwIEFYkCAsSBAWJAgLEoQFCcKCBGFBgrAgQViQICxIEBYkCAsShAUJwoIEYUGCsCBBWJAgLEgQFiQICxKEBQnCggRhQYKwIEFYkMjDGmbD7/wLQBfzsCbDr/0LQBd2srKPwpF/AejCTlaDk+nJPf8C0MXR+OjpwCZO4qV/EWgli1d5VDbcGaI338dflmHlH4dZvFk7CGgojMPjZVg2cRLf+YOARrJ4vhKVzeKsdbV2MFBHFm+OXx8/8l3lY1fzfCSijfwRw6bJH5gSFxrIn7TXmfyhaRav/QLAiizebD1T+Vlcc52tLQaYLJ5XXlPVGbt9tGcTXNgj/xTL4pl9ovlOOo1d3IdxeGmfqbhD5j/zRjH9B9pYcNHUfV/MAAAAAElFTkSuQmCC";

        static string LayoutDirectory {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"fse-tool\home"); }
        }

        // Package family names end in a hash of the publisher: the first 8 bytes of SHA-256 over the
        // UTF-16 publisher string, read as 65 bits (one zero bit appended) in Crockford-style base32.
        static string FamilyName {
            get {
                byte[] hash;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    hash = sha.ComputeHash(System.Text.Encoding.Unicode.GetBytes(Publisher));
                const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
                ulong bits = 0;
                for (int i = 0; i < 8; i++) bits = (bits << 8) | hash[i];
                var id = new System.Text.StringBuilder();
                for (int i = 0; i < 13; i++) {
                    int shift = 64 - 5 * (i + 1);
                    int chunk = shift >= 0 ? (int)((bits >> shift) & 31) : (int)((bits << -shift) & 31);
                    id.Append(alphabet[chunk]);
                }
                return PackageName + "_" + id;
            }
        }

        static string Aumid { get { return FamilyName + "!App"; } }

        public static bool IsPackaged() {
            int length = 0;
            // APPMODEL_ERROR_NO_PACKAGE (15700) means this process has no package identity.
            return GetCurrentPackageFullName(ref length, null) != 15700;
        }

        public static bool IsInstalled() {
            int count = 0, bufferLength = 0;
            GetPackagesByPackageFamily(FamilyName, ref count, IntPtr.Zero, ref bufferLength, IntPtr.Zero);
            return count > 0;
        }

        public static bool IsSelected() {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(GamingConfigPath)) {
                string selected = key == null ? null : key.GetValue("GamingHomeApp") as string;
                return string.Equals(selected, Aumid, StringComparison.OrdinalIgnoreCase);
            }
        }

        static void RunPowerShell(string command) {
            var info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"$ErrorActionPreference = 'Stop'; " + command.Replace("\"", "\\\"") + "\"") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
            };
            using (var process = Process.Start(info)) {
                process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(error.Length > 0 ? error : "PowerShell exited with code " + process.ExitCode + ".");
            }
        }

        static string PowerShellQuote(string value) { return "'" + value.Replace("'", "''") + "'"; }

        // Writes a copy of this exe with the PE optional-header Subsystem set to Windows GUI (2),
        // so XBOX mode launching the home app never opens a console window.
        static void WriteGuiCopy(string destination) {
            byte[] image = System.IO.File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName);
            int peOffset = BitConverter.ToInt32(image, 0x3C);
            if (BitConverter.ToUInt32(image, peOffset) != 0x00004550) throw new InvalidOperationException("Could not read this exe's PE header.");
            int subsystemOffset = peOffset + 24 + 68;
            image[subsystemOffset] = 2;
            image[subsystemOffset + 1] = 0;
            System.IO.File.WriteAllBytes(destination, image);
        }

        public static void Install() {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock")) {
                object value = key == null ? null : key.GetValue("AllowDevelopmentWithoutDevLicense");
                if (!(value is int) || (int)value != 1)
                    throw new InvalidOperationException("Developer Mode is off. Turn it on in Settings > System > Advanced > Developer Mode, then run 'fse-tool home install' again.");
            }
            string layout = LayoutDirectory;
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(layout, "Assets"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(layout, "Public"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(layout, "AppxManifest.xml"), Manifest);
            System.IO.File.WriteAllText(System.IO.Path.Combine(layout, "CustomCapability.SCCD"), Sccd);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(layout, @"Assets\Logo.png"), Convert.FromBase64String(LogoPng));
            WriteGuiCopy(System.IO.Path.Combine(layout, "fse-tool.exe"));
            RunPowerShell("Add-AppxPackage -Register " + PowerShellQuote(System.IO.Path.Combine(layout, "AppxManifest.xml")) + " -ForceUpdateFromAnyVersion -ForceApplicationShutdown");
            if (!IsInstalled()) throw new InvalidOperationException("Windows did not report the registered home app package.");
            System.Console.WriteLine(IsSelected()
                ? "Installed: fse-tool home app updated."
                : "Installed: select 'FSE Tool' in Settings > Gaming > XBOX mode > Choose home app.");
        }

        public static void Uninstall() {
            if (IsSelected())
                throw new InvalidOperationException("FSE Tool is the selected home app. Choose another one in Settings > Gaming > XBOX mode first; removing it while selected can break Task View.");
            if (IsInstalled()) RunPowerShell("Get-AppxPackage -Name " + PowerShellQuote(PackageName) + " | Remove-AppxPackage");
            if (System.IO.Directory.Exists(LayoutDirectory)) System.IO.Directory.Delete(LayoutDirectory, true);
            System.Console.WriteLine("Uninstalled: fse-tool home app removed.");
        }

        public static string Status() {
            return !IsInstalled() ? "NotInstalled" : IsSelected() ? "Selected" : "Installed";
        }

        // Joins arguments back into one command line, quoting the way CommandLineToArgvW splits them.
        public static string JoinArguments(string[] args, int start) {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = start; i < args.Length; i++) {
                string arg = args[i];
                if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) { parts.Add(arg); continue; }
                var quoted = new System.Text.StringBuilder("\"");
                int backslashes = 0;
                foreach (char c in arg) {
                    if (c == '\\') { backslashes++; continue; }
                    quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
                    backslashes = 0;
                    quoted.Append(c);
                }
                quoted.Append('\\', backslashes * 2).Append('"');
                parts.Add(quoted.ToString());
            }
            return string.Join(" ", parts.ToArray());
        }

        public static void SetDefault(string target, string arguments) {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsPath)) {
                key.SetValue("HomeTarget", target);
                key.SetValue("HomeArguments", arguments);
            }
            System.Console.WriteLine("Home target: " + (arguments.Length > 0 ? target + " " + arguments : target));
        }

        public static void SetPending(string target, string arguments) {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsPath)) {
                key.SetValue("PendingTarget", target);
                key.SetValue("PendingArguments", arguments);
                key.SetValue("PendingTime", DateTime.UtcNow.Ticks, Microsoft.Win32.RegistryValueKind.QWord);
            }
        }

        public static void ClearPending() {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsPath, true)) {
                if (key == null) return;
                key.DeleteValue("PendingTarget", false);
                key.DeleteValue("PendingArguments", false);
                key.DeleteValue("PendingTime", false);
            }
        }

        public static void Launch(string target, string arguments) {
            target = Environment.ExpandEnvironmentVariables(target);
            var info = new ProcessStartInfo(target, Environment.ExpandEnvironmentVariables(arguments)) { UseShellExecute = true };
            if (System.IO.File.Exists(target)) info.WorkingDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target));
            Process.Start(info);
        }

        // Entry point when XBOX mode starts the packaged copy: open a pending target from
        // 'fse-tool start --app' if one is fresh, otherwise the configured default.
        public static int RunAsHome(string[] args) {
            string target = DefaultTarget, arguments = "";
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsPath)) {
                    if (key != null) {
                        string home = key.GetValue("HomeTarget") as string;
                        if (!string.IsNullOrEmpty(home)) { target = home; arguments = key.GetValue("HomeArguments") as string ?? ""; }
                        string pending = key.GetValue("PendingTarget") as string;
                        object time = key.GetValue("PendingTime");
                        if (!string.IsNullOrEmpty(pending) && time is long && (DateTime.UtcNow - new DateTime((long)time, DateTimeKind.Utc)).TotalSeconds < PendingLifetimeSeconds) {
                            target = pending;
                            arguments = key.GetValue("PendingArguments") as string ?? "";
                        }
                    }
                }
                ClearPending();
                Launch(target, arguments);
                Log("Launched " + target + " " + arguments + " (args: " + JoinArguments(args, 0) + ")");
                return 0;
            } catch (Exception error) {
                Log("Failed to launch " + target + " " + arguments + ": " + error.Message);
                return 1;
            }
        }

        static void Log(string message) {
            try {
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fse-tool");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "home.log"), DateTime.Now.ToString("s") + " " + message.Trim() + Environment.NewLine);
            } catch (Exception) { }
        }
    }
}

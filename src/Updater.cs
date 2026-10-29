using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace XboxFseDirect {
    // Replaces this exe with the latest GitHub release. Windows lets a running exe be renamed but not
    // overwritten, so the old one moves aside to fse-tool.exe.old and is deleted on the next run.
    public static class Updater {
        const string LatestUrl = "https://github.com/lmrisdal/fse-tool/releases/latest";
        const string DownloadUrl = "https://github.com/lmrisdal/fse-tool/releases/download/{0}/fse-tool.exe";

        static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }

        public static void DeleteOldExe() {
            try {
                string old = ExePath + ".old";
                if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            } catch (Exception) { }
        }

        // The latest-release page redirects to .../releases/tag/<tag>, which avoids the API's rate limit.
        static string LatestTag() {
            var request = (HttpWebRequest)WebRequest.Create(LatestUrl);
            request.AllowAutoRedirect = false;
            request.Method = "HEAD";
            request.UserAgent = "fse-tool/" + BuildInfo.Version;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                string location = response.Headers["Location"] ?? "";
                var match = Regex.Match(location, @"/releases/tag/([^/?#]+)$");
                if (!match.Success) throw new InvalidOperationException("Could not find the latest release (HTTP " + (int)response.StatusCode + ").");
                return Uri.UnescapeDataString(match.Groups[1].Value);
            }
        }

        // True when release 'latest' (never a prerelease) is newer than 'current'. Builds after a tag,
        // such as 0.1.3-5-gabc1234-dirty from git describe, count as that tag; 0.2.0-beta.1 is older than 0.2.0.
        static bool IsNewer(string latest, string current) {
            var latestMatch = Regex.Match(latest, @"^v?(\d+)\.(\d+)\.(\d+)");
            var currentMatch = Regex.Match(current, @"^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$");
            if (!latestMatch.Success) throw new InvalidOperationException("Could not read the latest release version '" + latest + "'.");
            if (!currentMatch.Success)
                throw new InvalidOperationException("This is a development build (" + current + "), so it can't be compared with " + latest + ". Install a release with install.ps1 instead.");
            for (int i = 1; i <= 3; i++) {
                int difference = int.Parse(latestMatch.Groups[i].Value).CompareTo(int.Parse(currentMatch.Groups[i].Value));
                if (difference != 0) return difference > 0;
            }
            string suffix = currentMatch.Groups[4].Value;
            return suffix.Length > 0 && !Regex.IsMatch(suffix, @"^(\d+-g[0-9a-f]+)?(-?dirty)?$");
        }

        static string RunExe(string path, string arguments) {
            var info = new ProcessStartInfo(path, arguments) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
            };
            using (var process = Process.Start(info)) {
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(error.Result.Trim().Length > 0 ? error.Result.Trim() : path + " exited with code " + process.ExitCode + ".");
                return output.Trim();
            }
        }

        static bool CanWrite(string directory) {
            try {
                using (System.IO.File.Create(System.IO.Path.Combine(directory, System.IO.Path.GetRandomFileName()), 1, System.IO.FileOptions.DeleteOnClose)) { }
                return true;
            } catch (UnauthorizedAccessException) {
                return false;
            }
        }

        static bool IsAdministrator() {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        // Runs 'update --elevated' through UAC. The elevated copy has no console of ours, so it writes its
        // output to a file that is printed here.
        static void ReplaceElevated(string tag) {
            string log = System.IO.Path.GetTempFileName();
            try {
                var info = new ProcessStartInfo(ExePath, "update --elevated \"" + tag + "\" \"" + log + "\"") {
                    UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
                };
                int exitCode;
                try {
                    using (var process = Process.Start(info)) {
                        process.WaitForExit();
                        exitCode = process.ExitCode;
                    }
                } catch (System.ComponentModel.Win32Exception error) {
                    // 1223 is ERROR_CANCELLED: the UAC prompt was declined.
                    if (error.NativeErrorCode == 1223)
                        throw new InvalidOperationException("Update cancelled. Writing to " + System.IO.Path.GetDirectoryName(ExePath) + " needs administrator rights.");
                    throw;
                }
                string output = System.IO.File.ReadAllText(log).Trim();
                if (exitCode != 0) throw new InvalidOperationException(output.Length > 0 ? output : "The elevated update exited with code " + exitCode + ".");
                if (output.Length > 0) System.Console.WriteLine(output);
            } finally {
                try { System.IO.File.Delete(log); } catch (Exception) { }
            }
        }

        // Entry point for the elevated copy started by ReplaceElevated.
        public static int RunElevated(string tag, string log) {
            using (var writer = new System.IO.StreamWriter(log, false) { AutoFlush = true }) {
                System.Console.SetOut(writer);
                try {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    Replace(tag);
                    return 0;
                } catch (Exception error) {
                    writer.WriteLine(error.Message.Split(new[] { '\r', '\n' }, 2)[0].Trim());
                    return 1;
                }
            }
        }

        public static void Update() {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string current = BuildInfo.Version;
            string tag = LatestTag();
            if (!IsNewer(tag, current)) {
                System.Console.WriteLine("Up to date: fse-tool " + current + " is the latest version.");
                return;
            }

            // A Program Files install needs administrator rights to swap the exe. Only that step is
            // elevated: the home app is registered per user, so it is refreshed from this process.
            if (!CanWrite(System.IO.Path.GetDirectoryName(ExePath)) && !IsAdministrator()) ReplaceElevated(tag);
            else Replace(tag);

            // The home app runs its own copy of the exe, so refresh it from the new one.
            if (HomeApp.IsInstalled()) {
                try {
                    RunExe(ExePath, "home install");
                    System.Console.WriteLine("Updated: fse-tool home app refreshed.");
                } catch (Exception error) {
                    System.Console.Error.WriteLine("Warning: Could not update the fse-tool home app (" + error.Message.Split(new[] { '\r', '\n' }, 2)[0].Trim() + "). Run 'fse-tool home install' to retry.");
                }
            }
        }

        static void Replace(string tag) {
            string current = BuildInfo.Version;
            string exe = ExePath;
            string downloaded = exe + ".new", old = exe + ".old";
            System.Console.WriteLine("Downloading fse-tool " + tag.TrimStart('v') + "...");
            try {
                using (var client = new WebClient()) {
                    client.Headers[HttpRequestHeader.UserAgent] = "fse-tool/" + current;
                    client.DownloadFile(string.Format(DownloadUrl, Uri.EscapeDataString(tag)), downloaded);
                }
            } catch (UnauthorizedAccessException) {
                throw new InvalidOperationException("Can't write to " + System.IO.Path.GetDirectoryName(exe) + ". Run 'fse-tool update' from an administrator terminal.");
            } catch (WebException error) {
                if (System.IO.File.Exists(downloaded)) System.IO.File.Delete(downloaded);
                if (error.InnerException is UnauthorizedAccessException)
                    throw new InvalidOperationException("Can't write to " + System.IO.Path.GetDirectoryName(exe) + ". Run 'fse-tool update' from an administrator terminal.");
                throw;
            }

            // Make sure the download runs and is the release it claims to be before swapping it in.
            string version;
            try {
                version = RunExe(downloaded, "version");
            } catch (Exception) {
                System.IO.File.Delete(downloaded);
                throw new InvalidOperationException("The downloaded fse-tool.exe did not run.");
            }
            if (version != tag.TrimStart('v')) {
                System.IO.File.Delete(downloaded);
                throw new InvalidOperationException("The downloaded fse-tool.exe reports version " + version + ", expected " + tag.TrimStart('v') + ".");
            }

            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            System.IO.File.Move(exe, old);
            try {
                System.IO.File.Move(downloaded, exe);
            } catch {
                System.IO.File.Move(old, exe);
                throw;
            }
            System.Console.WriteLine("Updated: fse-tool " + current + " -> " + version + ".");
        }
    }
}

using System;
using System.Reflection;

namespace XboxFseDirect {
    public static class BuildInfo {
        // Set by build.ps1 through AssemblyInformationalVersion; "dev" when compiled without it.
        public static string Version {
            get {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                return attributes.Length > 0 ? ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion : "dev";
            }
        }
    }
}

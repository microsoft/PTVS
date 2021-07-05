// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Interpreters {
    class PythonRegistrySearch {
        public const string PythonCoreCompanyDisplayName = "Python Software Foundation";
        public const string PythonCoreSupportUrl = "https://www.python.org/";
        public const string PythonCoreCompany = "PythonCore";

        public const string CompanyPropertyKey = "Company";
        public const string SupportUrlPropertyKey = "SupportUrl";

        private readonly HashSet<string> _seenIds;
        private readonly List<PythonInterpreterInformation> _info;

        public PythonRegistrySearch() {
            _seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _info = new List<PythonInterpreterInformation>();
        }

        public static IEnumerable<PythonInterpreterInformation> PerformDefaultSearch() {
            var search = new PythonRegistrySearch();

            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Python")) {
                search.Search(key, Environment.Is64BitOperatingSystem ? InterpreterArchitecture.Unknown : InterpreterArchitecture.x86);
            }

            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = root.OpenSubKey("Software\\Python")) {
                search.Search(key, InterpreterArchitecture.x86);
            }

            if (Environment.Is64BitOperatingSystem) {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = root.OpenSubKey("Software\\Python")) {
                    search.Search(key, InterpreterArchitecture.x64);
                }
            }

            return search.Interpreters;
        }

        public IEnumerable<PythonInterpreterInformation> Interpreters => _info;

        public void Search(RegistryKey root, InterpreterArchitecture assumedArch) {
            if (root == null) {
                return;
            }

            var companies = GetSubkeys(root);
            if (companies == null) {
                return;
            }

            foreach (var company in companies) {
                if ("PyLauncher".Equals(company, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                bool pythonCore = PythonCoreCompany.Equals(company, StringComparison.OrdinalIgnoreCase);

                using (var companyKey = root.OpenSubKey(company)) {
                    if (companyKey == null) {
                        continue;
                    }

                    var companyDisplay = companyKey.GetValue("DisplayName") as string;
                    var companySupportUrl = companyKey.GetValue("SupportUrl") as string;

                    if (pythonCore) {
                        companyDisplay = companyDisplay ?? PythonCoreCompanyDisplayName;
                        companySupportUrl = companySupportUrl ?? PythonCoreSupportUrl;
                    } else {
                        companyDisplay = companyDisplay ?? company;
                    }

                    var tags = GetSubkeys(companyKey);
                    if (tags == null) {
                        continue;
                    }
                    foreach (var tag in tags) {
                        using (var tagKey = companyKey.OpenSubKey(tag))
                        using (var installKey = tagKey?.OpenSubKey("InstallPath")) {
                            var config = TryReadConfiguration(company, tag, tagKey, installKey, pythonCore, assumedArch);
                            if (config == null) {
                                continue;
                            }

                            if (_seenIds.Add(config.Id)) {
                                var supportUrl = tagKey.GetValue("SupportUrl") as string ?? companySupportUrl;

                                // We don't want to send people to http://python.org, even
                                // if that's what is in the registry, so catch and fix it.
                                if (!string.IsNullOrEmpty(supportUrl)) {
                                    var url = supportUrl.TrimEnd('/');
                                    if (url.Equals("http://www.python.org", StringComparison.OrdinalIgnoreCase) ||
                                        url.Equals("http://python.org", StringComparison.OrdinalIgnoreCase)) {
                                        supportUrl = PythonCoreSupportUrl;
                                    }
                                }

                                var info = new PythonInterpreterInformation(config, companyDisplay, companySupportUrl, supportUrl);
                                _info.Add(info);
                            }
                        }
                    }
                }
            }
        }

        private InterpreterConfiguration TryReadConfiguration(
            string company,
            string tag,
            RegistryKey tagKey,
            RegistryKey installKey,
            bool pythonCoreCompatibility,
            InterpreterArchitecture assumedArch
        ) {
            if (tagKey == null || installKey == null) {
                return null;
            }

            string prefixPath, exePath, exewPath;
            try {
                prefixPath = PathUtils.NormalizePath(installKey.GetValue(null) as string);
                exePath = PathUtils.NormalizePath(installKey.GetValue("ExecutablePath") as string);
                exewPath = PathUtils.NormalizePath(installKey.GetValue("WindowedExecutablePath") as string);
            } catch (ArgumentException ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                return null;
            }
            if (pythonCoreCompatibility && !string.IsNullOrEmpty(prefixPath)) {
                if (string.IsNullOrEmpty(exePath)) {
                    try {
                        exePath = PathUtils.GetAbsoluteFilePath(prefixPath, CPythonInterpreterFactoryConstants.ConsoleExecutable);
                    } catch (ArgumentException) {
                    }
                }
                if (string.IsNullOrEmpty(exewPath)) {
                    try {
                        exewPath = PathUtils.GetAbsoluteFilePath(prefixPath, CPythonInterpreterFactoryConstants.WindowsExecutable);
                    } catch (ArgumentException) {
                    }
                }
            }

            var version = tagKey.GetValue("Version") as string;
            if (pythonCoreCompatibility && string.IsNullOrEmpty(version) && tag.Length >= 3) {
                version = tag.Substring(0, 3);
            }

            Version sysVersion;
            var sysVersionString = tagKey.GetValue("SysVersion") as string;
            if (pythonCoreCompatibility && string.IsNullOrEmpty(sysVersionString) && tag.Length >= 3) {
                sysVersionString = tag.Substring(0, 3);
            }
            if (string.IsNullOrEmpty(sysVersionString) || !Version.TryParse(sysVersionString, out sysVersion)) {
                sysVersion = new Version(0, 0);
            }

            PythonLanguageVersion langVersion;
            try {
                langVersion = sysVersion.ToLanguageVersion();
            } catch (InvalidOperationException) {
                langVersion = PythonLanguageVersion.None;
                sysVersion = new Version(0, 0);
            }

            InterpreterArchitecture arch;
            if (!InterpreterArchitecture.TryParse(tagKey.GetValue("SysArchitecture", null) as string, out arch)) {
                arch = assumedArch;
            }

            if (arch == InterpreterArchitecture.Unknown && File.Exists(exePath)) {
                switch (NativeMethods.GetBinaryType(exePath)) {
                    case System.Reflection.ProcessorArchitecture.X86:
                        arch = InterpreterArchitecture.x86;
                        break;
                    case System.Reflection.ProcessorArchitecture.Amd64:
                        arch = InterpreterArchitecture.x64;
                        break;
                }
            }

            if (pythonCoreCompatibility && sysVersion != null && sysVersion < new Version(3, 5) && arch == InterpreterArchitecture.x86) {
                // Older versions of CPython did not include
                // "-32" in their Tag, so we will add it here
                // for uniqueness.
                tag += "-32";
            }

            var pathVar = tagKey.GetValue("PathEnvironmentVariable") as string ??
                CPythonInterpreterFactoryConstants.PathEnvironmentVariableName;

            var id = CPythonInterpreterFactoryConstants.GetInterpreterId(company, tag);

            var description = tagKey.GetValue("DisplayName") as string;
            if (string.IsNullOrEmpty(description)) {
                if (pythonCoreCompatibility) {
                    description = "Python {0}{1: ()}".FormatUI(version, arch);
                } else {
                    description = "{0} {1}".FormatUI(company, tag);
                }
            }

            return new InterpreterConfiguration(
                id,
                description,
                prefixPath,
                exePath,
                exewPath,
                pathVar,
                arch,
                sysVersion
            );
        }

        private static IList<string> GetSubkeys(RegistryKey key) {
            string[] subKeyNames = null;
            int delay = 10;
            for (int retries = 5; subKeyNames == null && retries > 0; --retries) {
                try {
                    subKeyNames = key.GetSubKeyNames();
                } catch (IOException) {
                    // Registry changed while enumerating subkeys. Give it a
                    // short period to settle down and try again.
                    // We are almost certainly being called from a background
                    // thread, so sleeping here is fine.
                    Thread.Sleep(delay);
                    delay *= 5;
                }
            }
            return subKeyNames;
        }
    }
}

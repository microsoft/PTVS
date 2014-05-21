/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Win32;

namespace AzureSetup {
    class Program {
        static readonly string[] SystemPackages = new[] {
            "IIS-WebServerRole",
            "IIS-WebServer",
            "IIS-CommonHttpFeatures",
            "IIS-StaticContent",
            "IIS-DefaultDocument",
            "IIS-DirectoryBrowsing",
            "IIS-HttpErrors",
            "IIS-HealthAndDiagnostics",
            "IIS-HttpLogging",
            "IIS-LoggingLibraries",
            "IIS-RequestMonitor",
            "IIS-Security",
            "IIS-RequestFiltering",
            "IIS-HttpCompressionStatic",
            "IIS-WebServerManagementTools",
            "IIS-ManagementConsole",
            "WAS-WindowsActivationService",
            "WAS-ProcessModel",
            "WAS-NetFxEnvironment",
            "WAS-ConfigurationAPI",
            "IIS-CGI"
        };

        static void Main(string[] args) {
            try {
                ConfigureFastCgi();
            } catch (Exception ex) {
                var logMessage = string.Format("Failed to initialize role.\r\n\r\nException:\r\n{0}", ex);

                bool wroteLog = false;
                try {
                    File.AppendAllText("AzureSetup.log", logMessage);
                    wroteLog = true;
                } catch {
                    Console.Error.WriteLine("Unable to write to log file.");
                }

                if (IsEmulated()) {
                    // In the emulator, so display some UI.
                    MessageBox.Show(
                        string.Format("Initialization failed due to a {0}:\r\n    {1}.", ex.GetType().Name, ex.Message),
                        "Python Tools for Visual Studio"
                    );
                }

                if (!wroteLog) {
                    // Failed to write to the log, so rethrow the exception so
                    // it will be shown on the console, will be added to the
                    // event log, and also sent via WER.
                    throw;
                }
            }
        }

        public static void ConfigureFastCgi() {
            var psi = new ProcessStartInfo();
            // pkgmgr.exe is deprecated for Windows 7/Server 2008R2 and later in
            // favor of dism.exe, and displays UI when invoked on these
            // platforms.
            // If dism.exe exists, we will use that; otherwise, we will fall
            // back on pkgmgr.exe. The feature names are identical between the
            // two programs, though the command lines differ slightly.
            var dismPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "dism.exe");
            var pkgMgrPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "PkgMgr.exe");

            // enable FastCGI in IIS
            if (File.Exists(dismPath)) {
                psi.FileName = dismPath;
                psi.Arguments = "/Online /NoRestart /Enable-Feature /FeatureName:" + string.Join(" /FeatureName:", SystemPackages);
            } else if (File.Exists(pkgMgrPath)) {
                psi.FileName = pkgMgrPath;
                psi.Arguments = "/quiet /iu:" + string.Join(";", SystemPackages);
            } else {
                Console.Error.WriteLine("Unable to install IIS FastCGI features.");
                psi = null;
            }

            if (psi != null) {
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (var proc = Process.Start(psi)) {
                    proc.WaitForExit();
                }
            }

            // Crack RoleModel.xml to figure out where our site lives...

            var roleRoot = Environment.GetEnvironmentVariable("RoleRoot");
            // In the cloud the RoleRoot is "E:" instead of "E:\", so we
            // manually add the backslash to ensure it is not interpreted as a
            // relative path.
            if (!roleRoot.EndsWith("\\")) {
                roleRoot = roleRoot + "\\";
            }

            var doc = new XPathDocument(Path.Combine(roleRoot, "RoleModel.xml"));

            var navigator = doc.CreateNavigator();
            XmlNamespaceManager mngr = new XmlNamespaceManager(new NameTable());
            mngr.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");

            var nodes = navigator.Select("/sd:RoleModel/sd:Sites/sd:Site", mngr);
            string physicalDir = null;
            foreach (XPathNavigator node in nodes) {
                // TODO: Multiple sites?
                physicalDir = node.GetAttribute("physicalDirectory", "");
                break;
            }

            if (!Path.IsPathRooted(physicalDir)) {
                physicalDir = Path.Combine(roleRoot, physicalDir);
            }

            nodes = navigator.Select("/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration']", mngr);
            bool isDebug = false;
            foreach (XPathNavigator node in nodes) {
                isDebug |= node.GetAttribute("value", "") == "Debug";
            }

            string interpreter = null, interpreterEmulated = null;
            if (physicalDir != null) {
                string fastCgiPath = "\"" + Path.Combine(physicalDir, "bin", "wfastcgi.py") + "\"";
                string settingsName = null, pythonPath = null;
                string setupCfg = Path.Combine(physicalDir, "bin", "AzureSetup.cfg");
                List<string> webpiInstalls = new List<string>();
                if (File.Exists(setupCfg)) {
                    try {
                        var allLines = File.ReadAllLines(setupCfg);
                        foreach (var line in allLines) {
                            var curOptions = line.Split(new[] { '=' }, 2);
                            if (curOptions.Length == 2) {
                                switch (curOptions[0]) {
                                    case "settings_module":
                                        settingsName = curOptions[1];
                                        break;
                                    case "python_path":
                                        pythonPath = Environment.ExpandEnvironmentVariables(
                                            Regex.Replace(
                                                curOptions[1],
                                                Regex.Escape("%RootDir%"),
                                                physicalDir,
                                                RegexOptions.IgnoreCase
                                            )
                                        );
                                        break;
                                    case "interpreter_path":
                                        interpreter = Environment.ExpandEnvironmentVariables(
                                            Regex.Replace(
                                                curOptions[1],
                                                Regex.Escape("%RootDir%"),
                                                physicalDir,
                                                RegexOptions.IgnoreCase
                                            )
                                        );
                                        break;
                                    case "interpreter_path_emulated":
                                        interpreterEmulated = Environment.ExpandEnvironmentVariables(
                                            Regex.Replace(
                                                curOptions[1],
                                                Regex.Escape("%RootDir%"),
                                                physicalDir,
                                                RegexOptions.IgnoreCase
                                            )
                                        );
                                        break;
                                    case "webpi_install":
                                        webpiInstalls.Add(
                                            curOptions[1]
                                        );
                                        break;
                                }
                            }
                        }
                    } catch (IOException) {
                    }
                }

                InstallWebPiProducts(physicalDir, webpiInstalls);

                if (interpreterEmulated != null && IsEmulated()) {
                    interpreter = interpreterEmulated;
                }

                if (String.IsNullOrEmpty(interpreter)) {
                    // TODO: Better discovery....
                    interpreter = Path.Combine(
                        Environment.GetEnvironmentVariable("SystemDrive") + "\\",
                        "Python27",
                        "python.exe"
                    );
                }

                if (settingsName == null) {
                    if (physicalDir.Length > 0 && physicalDir[physicalDir.Length - 1] == Path.DirectorySeparatorChar) {
                        settingsName = Path.GetFileName(physicalDir.Substring(0, physicalDir.Length - 1));
                    } else {
                        settingsName = Path.GetFileName(physicalDir);
                    }
                    settingsName += ".settings";
                }

                if (pythonPath == null) {
                    pythonPath = Path.Combine(physicalDir, "..");
                }

                UpdateIISAppCmd(interpreter, physicalDir, isDebug, fastCgiPath);

                UpdateWebConfig(interpreter, physicalDir, fastCgiPath);
            }
        }

        private static string GetWebPICommand(string physicalDir) {
            string webpiSetup, webpiCmdName, webpiMsiName;
            if (Environment.Is64BitOperatingSystem) {
                webpiSetup = "https://go.microsoft.com/fwlink/?linkid=226239";
                webpiCmdName = "webpicmd-x64.exe";
                webpiMsiName = "WebPlatformInstaller_amd64_en-US.msi";
            } else {
                webpiSetup = "https://go.microsoft.com/fwlink/?linkid=226238";
                webpiCmdName = "webpicmd.exe";
                webpiMsiName = "WebPlatformInstaller_x86_en-US.msi";
            }

            string path = Path.Combine(physicalDir, "bin", "WebPICmdLine.exe");
            if (File.Exists(path)) {
                return path;
            }
            path = Path.Combine(physicalDir, "bin", webpiCmdName);

            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft",
                "Web Platform Installer",
                webpiCmdName
            );
            if (File.Exists(path)) {
                return path;
            }

            var webpiMsi = Path.Combine(physicalDir, webpiMsiName);
            if (!File.Exists(webpiMsi)) {
                webpiMsi = Path.Combine(physicalDir, "bin", webpiMsiName);
                if (!File.Exists(webpiMsi)) {
                    var req = WebRequest.Create(webpiSetup);
                    var resp = req.GetResponse();
                    var stream = resp.GetResponseStream();
                    using (var msi = File.OpenWrite(webpiMsi)) {
                        var buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                            msi.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }

            using (var p = Process.Start("msiexec", "/quiet /i \"" + webpiMsi + "\" ADDLOCAL=ALL")) {
                p.WaitForExit();
            }

            return File.Exists(path) ? path : null;
        }

        private static void UpdateIISAppCmd(string interpreter, string physicalDir, bool isDebug, string fastCgiPath) {
            var appCmd = Environment.GetEnvironmentVariable("APPCMD");
            if (String.IsNullOrEmpty(appCmd)) {
                appCmd = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "System32", "inetsrv", "appcmd.exe");
            }
            interpreter = Escape(interpreter);
            fastCgiPath = Escape(fastCgiPath);

            RunAppCmd(appCmd,
                "set config /section:system.webServer/fastCGI \"/+[fullPath='{0}', arguments='{1}', instanceMaxRequests='{2}']\"",
                interpreter,
                fastCgiPath,
                isDebug ? "1" : "10000"
            );
        }

        private static void AppCmdSetProperty(string interpreter, string fastCgiPath, string appCmd, string propertyName, string value) {
            RunAppCmd(appCmd,
                "set config -section:system.webServer/fastCgi \"^/[fullPath='{0}', arguments='{1}'].{2}:{3}\"",
                interpreter,
                fastCgiPath,
                propertyName,
                value
            );
        }

        private static void AppCmdSetEnv(string interpreter, string fastCgiPath, string appCmd, string varName, string value) {
            RunAppCmd(appCmd,
                "set config -section:system.webServer/fastCgi /+\"[fullPath='{0}', arguments='{1}'].environmentVariables.[name='{2}',value='{3}']\"",
                interpreter,
                fastCgiPath,
                varName,
                Escape(value)
            );
        }


        private static string Escape(string interpreter) {
            // http://msdn.microsoft.com/en-us/library/bb776391(VS.85).aspx
            // 2n backslashes followed by a quotation mark produce n backslashes followed by a quotation mark.
            // (2n) + 1 backslashes followed by a quotation mark again produce n backslashes followed by a quotation mark.
            // n backslashes not followed by a quotation mark simply produce n backslashes.

            StringBuilder res = new StringBuilder();
            int backslashCount = 0;
            for (int i = 0; i < interpreter.Length; i++) {
                if (interpreter[i] == '"') {
                    for (int j = 0; j < backslashCount; j++) {
                        res.Append('\\');
                    }
                    res.Append("\\\"");
                    backslashCount = 0;
                } else if (interpreter[i] == '\\') {
                    backslashCount++;
                } else {
                    for (int j = 0; j < backslashCount; j++) {
                        res.Append('\\');
                    }
                    res.Append(interpreter[i]);
                    backslashCount = 0;
                }
            }
            return res.ToString();
        }

        private static void RunAppCmd(string appCmd, string argStr, params string[] args) {
            string fullArgs = String.Format(argStr, args);
            var appCmdEnd = appCmd.IndexOf("appcmd.exe", StringComparison.OrdinalIgnoreCase);
            if (appCmdEnd != -1) {
                if (appCmd[0] == '\"') {
                    // "D:\Program Files\IIS Express\appcmd.exe"
                    var closeQuote = appCmdEnd + "appcmd.exe".Length;
                    if (closeQuote < appCmd.Length &&
                        appCmd[closeQuote] == '"') {
                        appCmdEnd++;
                    }
                }
                var appCmdCmd = appCmd.Substring(0, appCmdEnd + "appcmd.exe".Length);
                fullArgs = fullArgs + appCmd.Substring(appCmdCmd.Length);

                var psi = new ProcessStartInfo(appCmdCmd, fullArgs);
                psi.UseShellExecute = false;
                var proc = Process.Start(psi);

                proc.WaitForExit();
            }
        }

        private static void UpdateWebConfig(string interpreter, string physicalDir, string fastCgiPath) {
            // patch web.config w/ the correct path to our fast cgi script
            var webCloudConfig = Path.Combine(physicalDir, "web.cloud.config");
            var webConfig = Path.Combine(physicalDir, "web.config");
            string readFrom;
            if (!IsEmulated() && File.Exists(webCloudConfig)) {
                readFrom = webCloudConfig;
            } else {
                readFrom = webConfig;
            }

            var text = File.ReadAllText(readFrom);            
            text = Regex.Replace(text, "%WFASTCGIPATH%", fastCgiPath.Replace("\"", "&quot;"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "%INTERPRETERPATH%", interpreter, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "%ROOTDIR%", physicalDir, RegexOptions.IgnoreCase);
            File.WriteAllText(webConfig, text);
        }

        private static void InstallWebPiProducts(string physicalDir, List<string> webpiInstalls) {
            if (IsEmulated() || webpiInstalls.Count == 0) {
                // Don't run installs in the emulator
                return;
            }

            // Get WebPI. This may download and install it if it is not already
            // available.
            string webpiCmdLinePath = GetWebPICommand(physicalDir);
            if (!File.Exists(webpiCmdLinePath)) {
                return;
            }

            // Deal w/ 32-bit vs 64-bit folder redirection of SYSTEM account...
            // http://blog.smarx.com/posts/windows-azure-startup-tasks-tips-tricks-and-gotchas
            // http://www.davidaiken.com/2011/01/19/running-azure-startup-tasks-as-a-real-user/

            // We will create a new directory and set our local app data to be there.
            var name = "AppData" + Guid.NewGuid();
            string dir;
            for (; ; ) {
                dir = Path.Combine(
                    Environment.GetEnvironmentVariable("SystemDrive") + "\\",
                    "SystemAppData" + Path.GetRandomFileName()
                );
                if (Directory.Exists(dir)) {
                    continue;
                }
                Directory.CreateDirectory(dir);
                break;
            }

            const string userShellFolders = ".DEFAULT\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\User Shell Folders";
            const string localAppData = "Local AppData";

            using (var key = Registry.Users.OpenSubKey(userShellFolders, true)) {
                var oldValue = key.GetValue(localAppData, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                key.SetValue(localAppData, dir);
                try {

                    // setup any installed products via WebPI...
                    foreach (var install in webpiInstalls) {
                        var paths = install.Split(new[] { ';' }, 2);
                        if (paths.Length == 2) {
                            using (var p = Process.Start(
                                webpiCmdLinePath,
                                "/Install " +
                                "/AcceptEula " +
                                "/Feeds:\"" + paths[0] + "\" " +
                                "/Products:" + paths[1]
                            )) {
                                p.WaitForExit();
                            }
                        }
                    }
                } finally {
                    key.SetValue(localAppData, oldValue, RegistryValueKind.ExpandString);
                }
            }
        }

        private static bool IsEmulated() {
            string emulated = Environment.GetEnvironmentVariable("EMULATED");
            bool res;
            if (String.IsNullOrEmpty(emulated) || !Boolean.TryParse(emulated, out res)) {
                return false;
            }

            return res;
        }
    }
}

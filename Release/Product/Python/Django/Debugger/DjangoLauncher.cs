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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Django.Debugger {
    /// <summary>
    /// Django launcher.  This wraps the default launcher and provides it with a different
    /// IPythonProject which launches manage.py with the appropriate options.  Upon a successful
    /// launch we will then automatically load the appropriate page into the users web browswer.
    /// </summary>
    [Export(typeof(IProjectLauncher))]
    class DjangoLauncher : IProjectLauncher {
        private int _testServerPort;

        private readonly IPythonProject _project;
        private readonly IProjectLauncher _defaultLauncher;
        private readonly DjangoPythonProject _djangoProject;
        [ThreadStatic] private static bool _debugLaunch;

        public DjangoLauncher(IPythonProject project, IPythonLauncherProvider defaultLauncher) {
            _project = project;
            _djangoProject = new DjangoPythonProject(this, project);
            _defaultLauncher = defaultLauncher.CreateLauncher(_djangoProject);
        }

        class DjangoPythonProject : IPythonProject {
            private readonly IPythonProject _realProject;
            private readonly DjangoLauncher _launcher;

            public DjangoPythonProject(DjangoLauncher launcher, IPythonProject realProject) {
                _launcher = launcher;
                _realProject = realProject;
            }

            #region IPythonProject Members

            public string GetProperty(string name) {
                switch (name) {
                    case CommonConstants.CommandLineArguments:
                        var userArgs = _realProject.GetProperty(CommonConstants.CommandLineArguments);
                        if (String.Equals(Path.GetFileName(_realProject.GetStartupFile()), "manage.py", StringComparison.OrdinalIgnoreCase)) {
                            _launcher._testServerPort = GetFreePort();

                            string commandLine = "runserver";
                            if (_debugLaunch) {
                                commandLine += " --noreload";
                            }
                            string settingsModule = _realProject.GetProperty(DjangoLauncherOptions.SettingModulesSetting);
                            if (!String.IsNullOrWhiteSpace(settingsModule)) {
                                commandLine += " --settings " + settingsModule;
                            }

                            if (!String.IsNullOrWhiteSpace(userArgs)) {
                                commandLine += " " + userArgs;
                            }

                            commandLine += " " + _launcher._testServerPort;
                            return commandLine;
                        }

                        return userArgs;
                    case "DjangoDebugging":
                        return "True";
                }
                return _realProject.GetProperty(name);
            }

            public void SetProperty(string name, string value) {
                _realProject.SetProperty(name, value);
            }

            public string GetWorkingDirectory() {
                return _realProject.GetWorkingDirectory();
            }

            public string GetStartupFile() {
                return _realProject.GetStartupFile();
            }

            public string ProjectDirectory {
                get { return _realProject.ProjectDirectory; }
            }

            public string ProjectName {
                get { return _realProject.ProjectName; }
            }

            public Interpreter.IPythonInterpreterFactory GetInterpreterFactory() {
                return _realProject.GetInterpreterFactory();
            }

            public bool Publish(PublishProjectOptions options) {
                return _realProject.Publish(options);
            }

            #endregion
        }

        #region IPythonLauncher Members

        public int LaunchProject(bool debug) {
            _debugLaunch = debug;
            int res = _defaultLauncher.LaunchProject(debug);
            if (ErrorHandler.Succeeded(res)) {
                ThreadPool.QueueUserWorkItem(StartBrowser, null);
            }
            return res;
        }

        private void StartBrowser(object dummy) {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Blocking = true;
            for (int i = 0; i < 20; i++) {
                try {
                    socket.Connect(IPAddress.Loopback, _testServerPort);
                    break;
                } catch {
                }
            }
            socket.Close();
            StartInBrowser("http://localhost:" + _testServerPort);
        }

        public int LaunchFile(string file, bool debug) {
            _debugLaunch = debug;
            int res = _defaultLauncher.LaunchFile(file, debug);
            if (ErrorHandler.Succeeded(res)) {
                StartInBrowser("http://localhost:" + _testServerPort);
            }
            return res;
        }


        #endregion

        public void StartInBrowser(string url) {
            // run the users default browser
            var handler = GetBrowserHandlerProgId();
            var browserCmd = (string)Registry.ClassesRoot.OpenSubKey(handler).OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("");

            if (browserCmd.IndexOf("%1") != -1) {
                browserCmd = browserCmd.Replace("%1", url);
            } else {
                browserCmd = browserCmd + " " + url;
            }
            bool inQuote = false;
            string cmdLine = null;
            for (int i = 0; i < browserCmd.Length; i++) {
                if (browserCmd[i] == '"') {
                    inQuote = !inQuote;
                }

                if (browserCmd[i] == ' ' && !inQuote) {
                    cmdLine = browserCmd.Substring(0, i);
                    break;
                }
            }
            if (cmdLine == null) {
                cmdLine = browserCmd;
            }

            Process.Start(cmdLine, browserCmd.Substring(cmdLine.Length));
        }

        private static string GetBrowserHandlerProgId() {
            try {
                return (string)Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Explorer").OpenSubKey("FileExts").OpenSubKey(".html").OpenSubKey("UserChoice").GetValue("Progid");
            } catch {
                return (string)Registry.ClassesRoot.OpenSubKey(".html").GetValue("");
            }
        }

        private static int GetFreePort() {
            return Enumerable.Range(new Random().Next(1200, 2000), 60000).Except(
                from connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                select connection.LocalEndPoint.Port
            ).First();
        }
    }
}

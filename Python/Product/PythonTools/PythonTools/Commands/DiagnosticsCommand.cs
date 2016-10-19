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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class DiagnosticsCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        private static readonly IEnumerable<string> InterestingDteProperties = new[] {
            "InterpreterId",
            "InterpreterVersion",
            "StartupFile",
            "WorkingDirectory",
            "PublishUrl",
            "SearchPath",
            "CommandLineArguments",
            "InterpreterPath"
        };
        
        private static readonly IEnumerable<string> InterestingProjectProperties = new[] {
            "ClusterRunEnvironment",
            "ClusterPublishBeforeRun",
            "ClusterWorkingDir",
            "ClusterMpiExecCommand",
            "ClusterAppCommand",
            "ClusterAppArguments",
            "ClusterDeploymentDir",
            "ClusterTargetPlatform",
            PythonWebLauncher.DebugWebServerTargetProperty,
            PythonWebLauncher.DebugWebServerTargetTypeProperty,
            PythonWebLauncher.DebugWebServerArgumentsProperty,
            PythonWebLauncher.DebugWebServerEnvironmentProperty,
            PythonWebLauncher.RunWebServerTargetProperty,
            PythonWebLauncher.RunWebServerTargetTypeProperty,
            PythonWebLauncher.RunWebServerArgumentsProperty,
            PythonWebLauncher.RunWebServerEnvironmentProperty,
            PythonWebPropertyPage.StaticUriPatternSetting,
            PythonWebPropertyPage.StaticUriRewriteSetting,
            PythonWebPropertyPage.WsgiHandlerSetting
        };

        private static readonly Regex InterestingApplicationLogEntries = new Regex(
            @"^Application: (devenv\.exe|.+?Python.+?\.exe|ipy(64)?\.exe)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        public DiagnosticsCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public override void DoCommand(object sender, EventArgs args) {
            var ui = _serviceProvider.GetUIThread();
            var cts = new CancellationTokenSource();
            bool skipAnalysisLog = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var dlg = new DiagnosticsWindow(_serviceProvider, Task.Run(() => GetData(ui, skipAnalysisLog, cts.Token), cts.Token));
            dlg.ShowModal();
            cts.Cancel();
        }

        private string GetData(UIThreadBase ui, bool skipAnalysisLog, CancellationToken cancel) {
            StringBuilder res = new StringBuilder();

            if (PythonToolsPackage.IsIpyToolsInstalled()) {
                res.AppendLine("WARNING: IpyTools is installed on this machine.  Having both IpyTools and Python Tools for Visual Studio installed will break Python editing.");
            }

            string pythonPathIsMasked = "";
            EnvDTE.DTE dte = null;
            IPythonInterpreterFactoryProvider[] knownProviders = null;
            IPythonLauncherProvider[] launchProviders = null;
            InMemoryLogger inMemLogger = null;
            ui.Invoke((Action)(() => {
                pythonPathIsMasked = _serviceProvider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath
                    ? " (masked)"
                    : "";
                dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                var model = _serviceProvider.GetComponentModel();
                knownProviders = model.GetExtensions<IPythonInterpreterFactoryProvider>().ToArray();
                launchProviders = model.GetExtensions<IPythonLauncherProvider>().ToArray();
                inMemLogger = model.GetService<InMemoryLogger>();
            }));

            res.AppendLine("Projects: ");

            var projects = dte.Solution.Projects;

            foreach (EnvDTE.Project project in projects) {
                cancel.ThrowIfCancellationRequested();

                string name;
                try {
                    // Some projects will throw rather than give us a unique
                    // name. They are not ours, so we will ignore them.
                    name = project.UniqueName;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    bool isPythonProject = false;
                    try {
                        isPythonProject = Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, project.Kind);
                    } catch (Exception ex2) when (!ex2.IsCriticalException()) {
                    }

                    if (isPythonProject) {
                        // Actually, it was one of our projects, so we do care
                        // about the exception. We'll add it to the output,
                        // rather than crashing.
                        res.AppendLine("    Project: " + ex.Message);
                        res.AppendLine("        Kind: Python");
                    }
                    continue;
                }
                res.AppendLine("    Project: " + name);

                if (Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, project.Kind)) {
                    res.AppendLine("        Kind: Python");

                    foreach (var prop in InterestingDteProperties) {
                        res.AppendLine("        " + prop + ": " + GetProjectProperty(project, prop));
                    }

                    var pyProj = project.GetPythonProject();
                    if (pyProj != null) {
                        ui.Invoke((Action)(() => {
                            foreach (var prop in InterestingProjectProperties) {
                                var propValue = pyProj.GetProjectProperty(prop);
                                if (propValue != null) {
                                    res.AppendLine("        " + prop + ": " + propValue);
                                }
                            }
                        }));

                        foreach (var factory in pyProj.InterpreterFactories) {
                            res.AppendLine();
                            res.AppendLine("        Interpreter: " + factory.Configuration.Description);
                            res.AppendLine("            Id: " + factory.Configuration.Id);
                            res.AppendLine("            Version: " + factory.Configuration.Version);
                            res.AppendLine("            Arch: " + factory.Configuration.Architecture);
                            res.AppendLine("            Prefix Path: " + factory.Configuration.PrefixPath ?? "(null)");
                            res.AppendLine("            Path: " + factory.Configuration.InterpreterPath ?? "(null)");
                            res.AppendLine("            Windows Path: " + factory.Configuration.WindowsInterpreterPath ?? "(null)");
                            res.AppendLine(string.Format("            Path Env: {0}={1}{2}",
                                factory.Configuration.PathEnvironmentVariable ?? "(null)",
                                Environment.GetEnvironmentVariable(factory.Configuration.PathEnvironmentVariable ?? ""),
                                pythonPathIsMasked
                            ));
                        }
                    }
                } else {
                    res.AppendLine("        Kind: " + project.Kind);
                }

                res.AppendLine();
            }

            res.AppendLine("Environments: ");
            foreach (var provider in knownProviders.MaybeEnumerate()) {
                cancel.ThrowIfCancellationRequested();

                res.AppendLine("    " + provider.GetType().FullName);
                foreach (var config in provider.GetInterpreterConfigurations()) {
                    res.AppendLine("        Id: " + config.Id);
                    res.AppendLine("        Factory: " + config.Description);
                    res.AppendLine("        Version: " + config.Version);
                    res.AppendLine("        Arch: " + config.Architecture);
                    res.AppendLine("        Prefix Path: " + config.PrefixPath ?? "(null)");
                    res.AppendLine("        Path: " + config.InterpreterPath ?? "(null)");
                    res.AppendLine("        Windows Path: " + config.WindowsInterpreterPath ?? "(null)");
                    res.AppendLine("        Path Env: " + config.PathEnvironmentVariable ?? "(null)");
                    res.AppendLine();
                }
            }

            res.AppendLine("Launchers:");
            foreach (var launcher in launchProviders.MaybeEnumerate()) {
                cancel.ThrowIfCancellationRequested();

                res.AppendLine("    Launcher: " + launcher.GetType().FullName);
                res.AppendLine("        " + launcher.Description);
                res.AppendLine("        " + launcher.Name);
                res.AppendLine();
            }

            try {
                res.AppendLine("Logged events/stats:");
                res.AppendLine(inMemLogger.ToString());
                res.AppendLine();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                res.AppendLine("  Failed to access event log.");
                res.AppendLine(ex.ToString());
                res.AppendLine();
            }

            try {
                res.AppendLine("System events:");

                var application = new EventLog("Application");
                var lastWeek = DateTime.Now.Subtract(TimeSpan.FromDays(7));
                foreach (var entry in application.Entries.Cast<EventLogEntry>()
                    .Where(e => e.InstanceId == 1026L)  // .NET Runtime
                    .Where(e => e.TimeGenerated >= lastWeek)
                    .Where(e => InterestingApplicationLogEntries.IsMatch(e.Message))
                    .OrderByDescending(e => e.TimeGenerated)
                ) {
                    res.AppendLine(string.Format("Time: {0:s}", entry.TimeGenerated));
                    using (var reader = new StringReader(entry.Message.TrimEnd())) {
                        for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                            res.AppendLine(line);
                        }
                    }
                    res.AppendLine();
                }

            } catch (Exception ex) when (!ex.IsCriticalException()) {
                res.AppendLine("  Failed to access event log.");
                res.AppendLine(ex.ToString());
                res.AppendLine();
            }

            res.AppendLine("Loaded assemblies:");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assem => assem.FullName)) {
                cancel.ThrowIfCancellationRequested();

                AssemblyFileVersionAttribute assemFileVersion;
                var error = "(null)";
                try {
                    assemFileVersion = assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)
                        .OfType<AssemblyFileVersionAttribute>()
                        .FirstOrDefault();
                } catch (Exception e) when (!e.IsCriticalException()) {
                    assemFileVersion = null;
                    error = string.Format("{0}: {1}", e.GetType().Name, e.Message);
                }

                res.AppendLine(string.Format("  {0}, FileVersion={1}",
                    assembly.FullName,
                    assemFileVersion?.Version ?? error
                ));
            }
            res.AppendLine();

            string globalAnalysisLog = PythonTypeDatabase.GlobalLogFilename;
            if (File.Exists(globalAnalysisLog)) {
                res.AppendLine("Global Analysis:");
                try {
                    res.AppendLine(File.ReadAllText(globalAnalysisLog));
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    res.AppendLine("Error reading the global analysis log.");
                    res.AppendLine("Please wait for analysis to complete and try again.");
                    res.AppendLine(ex.ToString());
                }
            }
            res.AppendLine();

            if (!skipAnalysisLog) {
                res.AppendLine("Environment Analysis Logs: ");
                foreach (var provider in knownProviders) {
                    foreach (var factory in provider.GetInterpreterFactories().OfType<IPythonInterpreterFactoryWithDatabase>()) {
                        cancel.ThrowIfCancellationRequested();

                        res.AppendLine(factory.Configuration.Description);
                        string analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(analysisLog)) {
                            res.AppendLine(analysisLog);
                        }
                        res.AppendLine();
                    }
                }
            }

            return res.ToString();
        }

        private static string GetProjectProperty(EnvDTE.Project project, string name) {
            try {
                return project.Properties.Item(name).Value.ToString();
            } catch {
                return "<undefined>";
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidDiagnostics; }
        }
    }
}

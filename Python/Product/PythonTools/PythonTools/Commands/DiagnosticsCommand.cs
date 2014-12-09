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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
            var dlg = new DiagnosticsForm(_serviceProvider, "Gathering data...");

            ThreadPool.QueueUserWorkItem(x => {
                var data = GetData();
                try {
                    dlg.BeginInvoke((Action)(() => {
                        dlg.Ready(data);
                    }));
                } catch (InvalidOperationException) {
                    // Window has been closed already
                }
            });
            dlg.ShowDialog();
        }

        private string GetData() {
            StringBuilder res = new StringBuilder();

            if (PythonToolsPackage.IsIpyToolsInstalled()) {
                res.AppendLine("WARNING: IpyTools is installed on this machine.  Having both IpyTools and Python Tools for Visual Studio installed will break Python editing.");
            }

            var pythonPathIsMasked = _serviceProvider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath ? " (masked)" : "";

            var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();

            var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
            res.AppendLine("Projects: ");

            var projects = dte.Solution.Projects;

            foreach (EnvDTE.Project project in projects) {
                string name;
                try {
                    // Some projects will throw rather than give us a unique
                    // name. They are not ours, so we will ignore them.
                    name = project.UniqueName;
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                    bool isPythonProject = false;
                    try {
                        isPythonProject = Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, project.Kind);
                    } catch (Exception ex2) {
                        if (ex2.IsCriticalException()) {
                            throw;
                        }
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
                        UIThread.Invoke(() => {
                            foreach (var prop in InterestingProjectProperties) {
                                var propValue = pyProj.GetProjectProperty(prop);
                                if (propValue != null) {
                                    res.AppendLine("        " + prop + ": " + propValue);
                                }
                            }
                        });

                        foreach (var factory in pyProj.Interpreters.GetInterpreterFactories()) {
                            res.AppendLine();
                            res.AppendLine("        Interpreter: " + factory.Description);
                            res.AppendLine("            Id: " + factory.Id);
                            res.AppendLine("            Version: " + factory.Configuration.Version);
                            if (interpreterService.FindInterpreter(factory.Id, factory.Configuration.Version) == null) {
                                res.AppendLine("            Arch: " + factory.Configuration.Architecture);
                                res.AppendLine("            Prefix Path: " + factory.Configuration.PrefixPath ?? "(null)");
                                res.AppendLine("            Path: " + factory.Configuration.InterpreterPath ?? "(null)");
                                res.AppendLine("            Windows Path: " + factory.Configuration.WindowsInterpreterPath ?? "(null)");
                                res.AppendLine("            Lib Path: " + factory.Configuration.LibraryPath ?? "(null)");
                                res.AppendLine(string.Format("            Path Env: {0}={1}{2}",
                                    factory.Configuration.PathEnvironmentVariable ?? "(null)",
                                    Environment.GetEnvironmentVariable(factory.Configuration.PathEnvironmentVariable ?? ""),
                                    pythonPathIsMasked
                                ));
                            }
                        }
                    }
                } else {
                    res.AppendLine("        Kind: " + project.Kind);
                }

                res.AppendLine();
            }

            res.AppendLine("Environments: ");
            foreach (var provider in interpreterService.KnownProviders) {
                res.AppendLine("    " + provider.GetType().FullName);
                foreach (var factory in provider.GetInterpreterFactories()) {
                    res.AppendLine("        Id: " + factory.Id);
                    res.AppendLine("        Factory: " + factory.Description);
                    res.AppendLine("        Version: " + factory.Configuration.Version);
                    res.AppendLine("        Arch: " + factory.Configuration.Architecture);
                    res.AppendLine("        Prefix Path: " + factory.Configuration.PrefixPath ?? "(null)");
                    res.AppendLine("        Path: " + factory.Configuration.InterpreterPath ?? "(null)");
                    res.AppendLine("        Windows Path: " + factory.Configuration.WindowsInterpreterPath ?? "(null)");
                    res.AppendLine("        Lib Path: " + factory.Configuration.LibraryPath ?? "(null)");
                    res.AppendLine("        Path Env: " + factory.Configuration.PathEnvironmentVariable ?? "(null)");
                    res.AppendLine();
                }
            }

            res.AppendLine("Launchers:");
            var launchProviders = _serviceProvider.GetComponentModel().GetExtensions<IPythonLauncherProvider>();
            foreach (var launcher in launchProviders) {
                res.AppendLine("    Launcher: " + launcher.GetType().FullName);
                res.AppendLine("        " + launcher.Description);
                res.AppendLine("        " + launcher.Name);
                res.AppendLine();
            }

            try {
                res.AppendLine("Logged events/stats:");
                var inMemLogger = _serviceProvider.GetComponentModel().GetService<InMemoryLogger>();
                res.AppendLine(inMemLogger.ToString());
                res.AppendLine();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
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

            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                res.AppendLine("  Failed to access event log.");
                res.AppendLine(ex.ToString());
                res.AppendLine();
            }

            res.AppendLine("Loaded assemblies:");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assem => assem.FullName)) {
                var assemFileVersion = assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false).OfType<AssemblyFileVersionAttribute>().FirstOrDefault();

                res.AppendLine(string.Format("  {0}, FileVersion={1}",
                    assembly.FullName,
                    assemFileVersion == null ? "(null)" : assemFileVersion.Version
                ));
            }
            res.AppendLine();

            string globalAnalysisLog = PythonTypeDatabase.GlobalLogFilename;
            if (File.Exists(globalAnalysisLog)) {
                res.AppendLine("Global Analysis:");
                try {
                    res.AppendLine(File.ReadAllText(globalAnalysisLog));
                } catch (Exception e) {
                    if (e.IsCriticalException()) {
                        throw;
                    }
                    res.AppendLine("Error reading: " + e);
                }
            }
            res.AppendLine();

            res.AppendLine("Environment Analysis Logs: ");
            foreach (var provider in interpreterService.KnownProviders) {
                foreach (var factory in provider.GetInterpreterFactories().OfType<IPythonInterpreterFactoryWithDatabase>()) {
                    res.AppendLine(factory.Description);
                    string analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(analysisLog)) {
                        res.AppendLine(analysisLog);
                    }
                    res.AppendLine();
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

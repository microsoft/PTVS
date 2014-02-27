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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class DiagnosticsCommand : Command {
        public override void DoCommand(object sender, EventArgs args) {
            var dlg = new DiagnosticsForm("Gathering data...");

            ThreadPool.QueueUserWorkItem(x => {
                var data = GetData();
                try {
                    dlg.BeginInvoke((Action)(() => {
                        dlg.TextBox.Text = data;
                        dlg.TextBox.SelectAll();
                    }));
                } catch (InvalidOperationException) {
                    // Window has been closed already
                }
            });
            dlg.ShowDialog();
        }

        private string GetData() {

            StringBuilder res = new StringBuilder();
            res.AppendLine("Use Ctrl-C to copy contents");
            res.AppendLine();

            if (PythonToolsPackage.IsIpyToolsInstalled()) {
                res.AppendLine("WARNING: IpyTools is installed on this machine.  Having both IpyTools and Python Tools for Visual Studio installed will break Python editing.");
            }

            var interpreterService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();

            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
            res.AppendLine("Projects: ");

            var projects = dte.Solution.Projects;
            var pyProjectKind = ("{" + PythonConstants.ProjectFactoryGuid + "}").ToLower();
            var interestingDteProperties = new[] { "InterpreterId", "InterpreterVersion", "StartupFile", "WorkingDirectory", "PublishUrl", "SearchPath", "CommandLineArguments", "InterpreterPath" };
            var interestingProjectProperties = new[] { "ClusterRunEnvironment", "ClusterPublishBeforeRun", "ClusterWorkingDir", "ClusterMpiExecCommand", "ClusterAppCommand", "ClusterAppArguments", "ClusterDeploymentDir", "ClusterTargetPlatform" };

            foreach (EnvDTE.Project project in projects) {
                string name;
                try {
                    // Some projects will throw rather than give us a unique
                    // name. They are not ours, so we will ignore them.
                    name = project.UniqueName;
                } catch (Exception ex) {
                    if (ErrorHandler.IsCriticalException(ex)) {
                        throw;
                    }
                    bool isPythonProject = false;
                    try {
                        isPythonProject = project.Kind.ToLower() == pyProjectKind;
                    } catch (Exception ex2) {
                        if (ErrorHandler.IsCriticalException(ex2)) {
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

                if (project.Kind.ToLower() == pyProjectKind) {
                    res.AppendLine("        Kind: Python");

                    foreach (var prop in interestingDteProperties) {
                        res.AppendLine("        " + prop + ": " + GetProjectProperty(project, prop));
                    }

                    var pyProj = project.GetPythonProject();
                    if (pyProj != null) {
                        foreach (var prop in interestingProjectProperties) {
                            var propValue = pyProj.GetProjectProperty(prop);
                            if (propValue != null) {
                                res.AppendLine("        " + prop + ": " + propValue);
                            }
                        }

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
                                res.AppendLine("            Path Env: " + factory.Configuration.PathEnvironmentVariable ?? "(null)");
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
            var launchProviders = PythonToolsPackage.ComponentModel.GetExtensions<IPythonLauncherProvider>();
            foreach (var launcher in launchProviders) {
                res.AppendLine("    Launcher: " + launcher.GetType().FullName);
                res.AppendLine("        " + launcher.Description);
                res.AppendLine("        " + launcher.Name);
                res.AppendLine();
            }

            res.AppendLine("Logged events/stats:");
            var inMemLogger = PythonToolsPackage.ComponentModel.GetService<InMemoryLogger>();
            res.AppendLine(inMemLogger.ToString());
            res.AppendLine();

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

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
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class DiagnosticsCommand : Command {
        public override void DoCommand(object sender, EventArgs args) {
            var dlg = new DiagnosticsForm("Gathering data...");

            ThreadPool.QueueUserWorkItem(x => {
                var data = GetData();
                dlg.BeginInvoke((Action)(() => {
                    dlg.TextBox.Text = data;
                    dlg.TextBox.SelectAll();
                }));
            });
            dlg.ShowDialog();
        }

        private string GetData() {

            StringBuilder res = new StringBuilder();
            res.AppendLine("Use Ctrl-C to copy contents");
            res.AppendLine();

            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
            res.AppendLine("Projects: ");

            var projects = dte.Solution.Projects;
            var pyProjectKind = ("{" + PythonConstants.ProjectFactoryGuid + "}").ToLower();
            var interestingDteProperties = new[] { "InterpreterId", "InterpreterVersion", "StartupFile", "WorkingDirectory", "PublishUrl", "SearchPath", "CommandLineArguments", "InterpreterPath" };
            var interestingProjectProperties = new[] { "ClusterRunEnvironment", "ClusterPublishBeforeRun", "ClusterWorkingDir", "ClusterMpiExecCommand", "ClusterAppCommand", "ClusterAppArguments", "ClusterDeploymentDir", "ClusterTargetPlatform" };

            foreach (EnvDTE.Project project in projects) {
                res.AppendLine("    Project: " + project.UniqueName);

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
                    }
                } else {
                    res.AppendLine("        Kind: " + project.Kind);
                }

                res.AppendLine();
            }

            res.AppendLine("Interpreters: ");
            var compModel = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var factoryProviders = compModel.GetExtensions<IPythonInterpreterFactoryProvider>();
            foreach (var provider in factoryProviders) {
                res.AppendLine("    " + provider.GetType().FullName);
                foreach (var factory in provider.GetInterpreterFactories()) {
                    res.AppendLine("        Id: " + factory.Id);
                    res.AppendLine("        Factory: " + factory.Description);
                    res.AppendLine("        Version: " + factory.Configuration.Version);
                    res.AppendLine("        Arch: " + factory.Configuration.Architecture);
                    res.AppendLine("        Path: " + factory.Configuration.InterpreterPath);
                    res.AppendLine("        Windows Path: " + factory.Configuration.WindowsInterpreterPath);
                    res.AppendLine("        Path Env: " + factory.Configuration.PathEnvironmentVariable);
                    res.AppendLine();

                    string analysisLog = Path.Combine(GetCompletionDatabaseDirPath(), factory.Id.ToString("D"), factory.Configuration.Version.ToString(), "AnalysisLog.txt");
                    if (File.Exists(analysisLog)) {
                        try {
                            res.AppendLine(File.ReadAllText(analysisLog));
                        } catch (Exception e) {
                            res.AppendLine("Error reading: " + e);
                        }
                    }
                }
            }

            res.AppendLine("Launchers:");
            var launchProviders = compModel.GetExtensions<IPythonLauncherProvider>();
            foreach (var launcher in launchProviders) {
                res.AppendLine("    Launcher: " + launcher.GetType().FullName);
                res.AppendLine("        " + launcher.Description);
                res.AppendLine("        " + launcher.Name);
                res.AppendLine();
            }

            string globalAnalysisLog = Path.Combine(GetCompletionDatabaseDirPath(), "AnalysisLog.txt");
            if (File.Exists(globalAnalysisLog)) {
                res.AppendLine("Global Analysis:");
                try {
                    res.AppendLine(File.ReadAllText(globalAnalysisLog));
                } catch (Exception e) {
                    res.AppendLine("Error reading: " + e);
                }
            }
            return res.ToString();
        }

        private string GetCompletionDatabaseDirPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python Tools\\CompletionDB"
            );
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

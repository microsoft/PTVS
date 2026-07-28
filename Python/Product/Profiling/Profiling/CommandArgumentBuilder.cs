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



namespace Microsoft.PythonTools.Profiling {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using Microsoft.PythonTools.Infrastructure;
    using Microsoft.PythonTools.Interpreter;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    internal class CommandArgumentBuilder {

        /// <summary>
        /// Constructs a <see cref="PythonProfilingCommandArgs"/> based on the provided profiling target.
        /// </summary>
        public PythonProfilingCommandArgs BuildCommandArgsFromTarget(ProfilingTarget target, IServiceProvider serviceProvider) {
            if (target == null) {
                return null;
            }

            try {
                ThreadHelper.ThrowIfNotOnUIThread();
                return SelectBuilder(target, serviceProvider);
            } catch (Exception ex) {
                Debug.Fail($"Error building command: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Select the appropriate builder based on the provided profiling target.
        /// </summary>
        private PythonProfilingCommandArgs SelectBuilder(ProfilingTarget target, IServiceProvider serviceProvider) {
            var projectTarget = target.ProjectTarget;
            var standaloneTarget = target.StandaloneTarget;

            if (projectTarget != null) {
                return BuildProjectCommandArgs(projectTarget, serviceProvider);
            } else if (standaloneTarget != null) {
                return BuildStandaloneCommandArgs(standaloneTarget, serviceProvider);
            }
            return null;
        }

        private PythonProfilingCommandArgs BuildProjectCommandArgs(ProjectTarget projectTarget, IServiceProvider serviceProvider) {
            if (serviceProvider == null) {
                return null;
            }

            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null) { 
                return null;
            }

            var project = solution.EnumerateLoadedPythonProjects()
                .SingleOrDefault(p => p.GetProjectIDGuidProperty() == projectTarget.TargetProject);

            if (project == null) {
                return null;
            }

            LaunchConfiguration config = null;
            try {
                config = project?.GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(serviceProvider, ex.HelpPage);
                return null;
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return null;
            } catch (IOException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return null;
            }
            if (config == null) {
                MessageBox.Show(Strings.ProjectInterpreterNotFound.FormatUI(project.GetNameProperty()), Strings.ProductTitle);
                return null;
            }

            if (string.IsNullOrEmpty(config.ScriptName)) {
                MessageBox.Show(Strings.NoProjectStartupFile, Strings.ProductTitle);
                return null;
            }

            if (string.IsNullOrEmpty(config.WorkingDirectory) || config.WorkingDirectory == ".") {
                config.WorkingDirectory = project.ProjectHome;
                if (string.IsNullOrEmpty(config.WorkingDirectory)) {
                    config.WorkingDirectory = Path.GetDirectoryName(config.ScriptName);
                }
            }

            return BuildDiagnosticsHubCommand(config, serviceProvider);
        }

        private PythonProfilingCommandArgs BuildStandaloneCommandArgs(StandaloneTarget standaloneTarget, IServiceProvider serviceProvider) {
            if (standaloneTarget == null || serviceProvider == null) {
                return null;
            }

            LaunchConfiguration config = null;

            if (standaloneTarget.InterpreterPath != null) {
                config = new LaunchConfiguration(null);
            }

            if (standaloneTarget.PythonInterpreter != null) {
                var registry = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                var interpreter = registry.FindConfiguration(standaloneTarget.PythonInterpreter.Id);
                if (interpreter == null) {
                    return null;
                }

                config = new LaunchConfiguration(interpreter);
            }

            config.InterpreterPath = standaloneTarget.InterpreterPath;
            config.ScriptName = standaloneTarget.Script;
            config.ScriptArguments = standaloneTarget.Arguments;
            config.WorkingDirectory = standaloneTarget.WorkingDirectory;

            return BuildDiagnosticsHubCommand(config, serviceProvider);
        }

        private static PythonProfilingCommandArgs BuildDiagnosticsHubCommand(LaunchConfiguration config, IServiceProvider serviceProvider) {
            var targetCommandLine = ProcessOutput.QuoteSingleArgument(config.ScriptName);
            if (!string.IsNullOrWhiteSpace(config.ScriptArguments)) {
                targetCommandLine = string.Join(" ", targetCommandLine, config.ScriptArguments);
            }

            return new PythonProfilingCommandArgs {
                PythonExePath = config.GetInterpreterPath(),
                WorkingDir = config.WorkingDirectory,
                ScriptPath = PythonToolsInstallPath.GetFile("diaghub_profile.py", typeof(CommandArgumentBuilder).Assembly),
                // DiagnosticsHub joins this array into the interpreter command line.
                // Keep the user's original argument string intact after the quoted script.
                Args = new[] { targetCommandLine },
                EnvVars = serviceProvider.GetPythonToolsService().GetFullEnvironment(config)
            };
        }
    }
}

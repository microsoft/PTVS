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

    internal class CommandBuilder {

        /// <summary>
        /// Constructs a <see cref="TargetCommand"/> based on the provided profiling target.
        /// </summary>
        public TargetCommand BuildCommandFromTarget(ProfilingTarget target) {
            if (target == null) {
                return null;
            }

            try {
                var pythonProfilingPackage = PythonProfilingPackage.Instance;
                var joinableTaskFactory = pythonProfilingPackage.JoinableTaskFactory;

                TargetCommand command = null;

                joinableTaskFactory.Run(async () => {
                    await joinableTaskFactory.SwitchToMainThreadAsync();

                    var name = target.GetProfilingName(pythonProfilingPackage, out var save);
                    var explorer = await pythonProfilingPackage.ShowPerformanceExplorerAsync();
                    var session = explorer.Sessions.AddTarget(target, name, save);

                    command = SelectCommandBuilder(target, session);

                });

                return command;
            } catch (Exception ex) {
                Debug.Fail($"Error building command: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates a <see cref="TargetCommand"/> based on the profiling target and session details.
        /// </summary>
        private TargetCommand SelectCommandBuilder(ProfilingTarget target, SessionNode session) {
            var projectTarget = target.ProjectTarget;
            var standaloneTarget = target.StandaloneTarget;

            if (projectTarget != null) {
                return BuildProjectCommand(projectTarget, session);
            } else if (standaloneTarget != null) {
                return BuildStandaloneCommand(standaloneTarget, session);
            }
            return null;
        }

        private TargetCommand BuildProjectCommand(ProjectTarget projectTarget, SessionNode session) {
            var solution = PythonProfilingPackage.Instance.Solution;
            var project = solution.EnumerateLoadedPythonProjects()
                .SingleOrDefault(p => p.GetProjectIDGuidProperty() == projectTarget.TargetProject);

            if (project == null) {
                return null;
            }

            LaunchConfiguration config = null;
            try {
                config = project?.GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(session._serviceProvider, ex.HelpPage);
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

            var pythonExePath = config.GetInterpreterPath();
            var scriptPath = string.Join(" ", ProcessOutput.QuoteSingleArgument(config.ScriptName), config.ScriptArguments);
            var workingDir = config.WorkingDirectory;
            var envVars = session._serviceProvider.GetPythonToolsService().GetFullEnvironment(config);

            var command = new TargetCommand {
                PythonExePath = pythonExePath,
                ScriptPath = scriptPath,
                WorkingDir = workingDir,
                Args = Array.Empty<string>(),
                EnvVars = envVars
            };
            return command;
        }

        private TargetCommand BuildStandaloneCommand(StandaloneTarget standaloneTarget, SessionNode session) {
            if (standaloneTarget == null) {
                return null;
            }

            LaunchConfiguration config = null;

            if (standaloneTarget.InterpreterPath != null) {
                config = new LaunchConfiguration(null);
            }

            if (standaloneTarget.PythonInterpreter != null) {
                var registry = session._serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
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

            var argsInput = standaloneTarget.Arguments;
            var parsedArgs = string.IsNullOrWhiteSpace(argsInput)
                        ? Array.Empty<string>()
                        : argsInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var envVars = session._serviceProvider.GetPythonToolsService().GetFullEnvironment(config);

            return new TargetCommand {
                PythonExePath = config.GetInterpreterPath(),
                WorkingDir = standaloneTarget.WorkingDirectory,
                ScriptPath = standaloneTarget.Script,
                Args = parsedArgs,
                EnvVars = envVars
            };
        }
    }
}



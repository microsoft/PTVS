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
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.PythonTools.Infrastructure;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// Implements a service to collect user input for profiling and convert to a <see cref="PythonProfilingCommandArgs"/>.
    /// </summary>
    [Export(typeof(IPythonProfilerCommandService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class PythonProfilerCommandService : IPythonProfilerCommandService {
        private const int MinimumSupportedPythonMinorVersion = 12;
        private const int MaximumSupportedPythonMinorVersion = 14;
        private static readonly TimeSpan InterpreterVersionTimeout = TimeSpan.FromSeconds(10);

        private readonly CommandArgumentBuilder _commandArgumentBuilder;
        private readonly IServiceProvider _serviceProvider;
        private readonly UserInputDialog _userInputDialog;

        [ImportingConstructor]
        public PythonProfilerCommandService(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider
        ) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _commandArgumentBuilder = new CommandArgumentBuilder();
            _userInputDialog = new UserInputDialog();
        }

        /// <summary>
        /// Collects user input and constructs a <see cref="PythonProfilingCommandArgs"/> object.
        /// </summary>
        /// <returns>
        /// A <see cref="PythonProfilingCommandArgs"/> object based on user input, or <c>null</c> if canceled.
        /// </returns>
        public async Task<IPythonProfilingCommandArgs> GetCommandArgsFromUserInput() {
            try {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var targetView = new ProfilingTargetView(_serviceProvider);

                if (_userInputDialog.ShowDialog(targetView, _serviceProvider)) {
                    var target = targetView.GetTarget();
                    var commandArgs = _commandArgumentBuilder.BuildCommandArgsFromTarget(target, _serviceProvider);
                    if (commandArgs == null) {
                        return null;
                    }

                    var pythonVersion = await GetPythonVersionAsync(commandArgs.PythonExePath);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (pythonVersion == null) {
                        MessageBox.Show(
                            Strings.ProfilingInterpreterVersionUnavailable.FormatUI(commandArgs.PythonExePath),
                            Strings.ProductTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                        return null;
                    }

                    if (!IsSupportedPythonVersion(pythonVersion)) {
                        MessageBox.Show(
                            Strings.ProfilingUnsupportedPythonVersion.FormatUI(
                                pythonVersion.Major,
                                pythonVersion.Minor
                            ),
                            Strings.ProductTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        return null;
                    }

                    return commandArgs;
                }
            } catch (Exception ex) {
                Debug.Fail($"Error displaying user input dialog: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private static bool IsSupportedPythonVersion(Version version) {
            return version.Major == 3 &&
                version.Minor >= MinimumSupportedPythonMinorVersion &&
                version.Minor <= MaximumSupportedPythonMinorVersion;
        }

        private static async Task<Version> GetPythonVersionAsync(string interpreterPath) {
            try {
                using (var output = ProcessOutput.RunHiddenAndCapture(
                    interpreterPath,
                    "-c",
                    "import sys; print('{}.{}'.format(sys.version_info[0], sys.version_info[1]))"
                )) {
                    var exited = await Task.Run(() => output.Wait(InterpreterVersionTimeout));
                    if (!exited) {
                        output.Kill();
                        return null;
                    }

                    Version version;
                    var versionText = output.ExitCode == 0
                        ? output.StandardOutputLines.FirstOrDefault()
                        : null;
                    return Version.TryParse(versionText, out version) ? version : null;
                }
            } catch (ArgumentException ex) {
                Debug.WriteLine($"Failed to query Python version: {ex.Message}");
                return null;
            } catch (Win32Exception ex) {
                Debug.WriteLine($"Failed to query Python version: {ex.Message}");
                return null;
            } catch (InvalidOperationException ex) {
                Debug.WriteLine($"Failed to query Python version: {ex.Message}");
                return null;
            }
        }
    }
}

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
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// Implements a service to collect user input for profiling and convert to a <see cref="PythonProfilingCommandArgs"/>.
    /// </summary>
    [Export(typeof(IPythonProfilerCommandService))]
    class PythonProfilerCommandService : IPythonProfilerCommandService {
        private readonly CommandArgumentBuilder _commandArgumentBuilder;
        private readonly UserInputDialog _userInputDialog;

        public PythonProfilerCommandService() {
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
                var pythonProfilingPackage = await GetPythonProfilingPackageAsync();
                if (pythonProfilingPackage == null) {
                    return null;
                    
                }
                var targetView = new ProfilingTargetView(pythonProfilingPackage);

                if (_userInputDialog.ShowDialog(targetView, pythonProfilingPackage)) {
                    var target = targetView.GetTarget();
                    return _commandArgumentBuilder.BuildCommandArgsFromTarget(target, pythonProfilingPackage);
                }
            } catch (Exception ex) {
                Debug.Fail($"Error displaying user input dialog: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private async Task<PythonProfilingPackage> GetPythonProfilingPackageAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var shell = await ServiceProvider.GetGlobalServiceAsync(typeof(SVsShell)) as IVsShell;
            if (shell != null) {
                var packageGuid = typeof(PythonProfilingPackage).GUID;
                int hr = shell.LoadPackage(ref packageGuid, out var packageObj);

                Debug.WriteLine($"LoadPackage result: {hr}"); // Log HRESULT result

                if (packageObj != null) {
                    return packageObj as PythonProfilingPackage;
                }
            }
            return null;
        }
    }
}

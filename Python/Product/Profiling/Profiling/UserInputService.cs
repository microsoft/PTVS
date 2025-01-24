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
    using System.IO;
    using System.Linq;
    using System.Windows;
    using Microsoft.PythonTools.Infrastructure;
    using Microsoft.PythonTools.Interpreter;

    /// <summary>
    /// Implements a service to collect user input for profiling and generate a <see cref="TargetCommand"/>.
    /// </summary>
    [Export(typeof(IUserInputService))]
    class UserInputService : IUserInputService {
        private readonly CommandBuilder _commandBuilder;
        private readonly UserDialog _userDialog;

        [ImportingConstructor]
        public UserInputService() {
            _commandBuilder = new CommandBuilder();
            _userDialog = new UserDialog();
        }

        /// <summary>
        /// Collects user input and constructs a <see cref="TargetCommand"/> object.
        /// </summary>
        /// <returns>
        /// A <see cref="TargetCommand"/> object based on user input, or <c>null</c> if canceled.
        /// </returns>
        public TargetCommand GetCommandFromUserInput() {
            try {
                var pythonProfilingPackage = PythonProfilingPackage.Instance;
                var targetView = new ProfilingTargetView(pythonProfilingPackage);

                if (_userDialog.ShowDialog(targetView)) {
                    var target = targetView.GetTarget();
                    return _commandBuilder.BuildCommandFromTarget(target);
                }
                return null;
            } catch (Exception ex) {
                Debug.Fail($"Error displaying user input dialog: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}

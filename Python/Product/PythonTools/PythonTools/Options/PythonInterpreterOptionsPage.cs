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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Provides the dialog page for configuring the interpreter options.  Accessible via automation via 
    /// dte.get_Properties("Python Tools", "Interpreters") and exposes the following values:
    /// 
    ///     DefaultInterpreter  -       Guid   - The ID of the default interpreter
    ///     DefaultInterpreterVersion - string - The version number of the default interpreter
    ///     
    /// New in 1.5.
    /// </summary>
    [ComVisible(true)]
    public sealed class PythonInterpreterOptionsPage : PythonDialogPage {
        private PythonInterpreterOptionsControl _window;
        private IInterpreterRegistry _service;

        public PythonInterpreterOptionsPage()
            : base("Interpreters") {
        }

        internal static IPythonInterpreterFactory NextOptionsSelection { get; set; }

        void InterpretersChanged(object sender, EventArgs e) {
            LoadSettingsFromStorage();
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                return GetWindow();
            }
        }

        private PythonInterpreterOptionsControl GetWindow() {
            if (_window == null) {
                _service = ComponentModel.GetService<IInterpreterRegistry>();
                _service.InterpretersChanged += InterpretersChanged;
                _window = new PythonInterpreterOptionsControl(ComponentModel.GetService<SVsServiceProvider>());
            }
            return _window;
        }

        public override void ResetSettings() {
            PyService.GlobalInterpreterOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            if (_window != null) {
                _window.UpdateInterpreters();
            }
        }

        public override void SaveSettingsToStorage() {
            var defaultInterpreter = GetWindow().DefaultInterpreter;

            if (defaultInterpreter != null) {
                Version ver;
                if (defaultInterpreter is InterpreterPlaceholder) {
                    ver = Version.Parse(PyService.GetInterpreterOptions(defaultInterpreter).Version ?? "2.7");
                } else {
                    ver = defaultInterpreter.Configuration.Version;
                }

                PyService.GlobalInterpreterOptions.DefaultInterpreter = defaultInterpreter.Configuration.Id;
            } else {
                PyService.GlobalInterpreterOptions.DefaultInterpreter = string.Empty;
            }

            PyService.SaveInterpreterOptions();
            PyService.GlobalInterpreterOptions.Save();
        }

    }
}

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
        private IInterpreterOptionsService _service;

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
                _service = ComponentModel.GetService<IInterpreterOptionsService>();
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

                PyService.GlobalInterpreterOptions.DefaultInterpreter = defaultInterpreter.Id;
                PyService.GlobalInterpreterOptions.DefaultInterpreterVersion = ver;
            } else {
                PyService.GlobalInterpreterOptions.DefaultInterpreter = Guid.Empty;
                PyService.GlobalInterpreterOptions.DefaultInterpreterVersion = new Version();
            }

            PyService.SaveInterpreterOptions();
            PyService.GlobalInterpreterOptions.Save();
        }

        /// <summary>
        /// Gets or sets the default interpreter ID.
        /// </summary>
        /// <remarks
        /// The actual default will only be changed if an interpreter is
        /// available that matches both <see cref="DefaultInterpreter"/> and
        /// <see cref="DefaultInterpreterVersion"/>. These properties may be
        /// invalid if an interpreter does not exist and settings have not been
        /// saved or loaded recently.
        /// 
        /// Use <see cref="IInterpreterOptionsService"/> to accurately determine
        /// the default interpreter.
        /// </remarks>
        [Obsolete("Use PythonToolsService.GlobalInterpreterOptions instead")]
        public Guid DefaultInterpreter {
            get {
                return PyService.GlobalInterpreterOptions.DefaultInterpreter;
            }
            set {
                if (PyService.GlobalInterpreterOptions.DefaultInterpreter != value) {
                    PyService.GlobalInterpreterOptions.DefaultInterpreter = value;
                    PyService.GlobalInterpreterOptions.UpdateInterpreter();
                }
            }
        }

        /// <summary>
        /// Gets or sets the default interpreter version. This should be a
        /// string in "major.minor" format, for example, "3.2".
        /// </summary>
        /// <remarks
        /// The actual default will only be changed if an interpreter is
        /// available that matches both <see cref="DefaultInterpreter"/> and
        /// <see cref="DefaultInterpreterVersion"/>. These properties may be
        /// invalid if an interpreter does not exist and settings have not been
        /// saved or loaded recently.
        /// 
        /// Use <see cref="IInterpreterOptionsService"/> to accurately determine
        /// the default interpreter.
        /// </remarks>
        [Obsolete("Use PythonToolsService.GlobalInterpreterOptions instead")]
        public string DefaultInterpreterVersion {
            get {
                return PyService.GlobalInterpreterOptions.DefaultInterpreterVersion.ToString();
            }
            set {
                var ver = Version.Parse(value);
                if (PyService.GlobalInterpreterOptions.DefaultInterpreterVersion != ver) {
                    PyService.GlobalInterpreterOptions.DefaultInterpreterVersion = ver;
                    PyService.GlobalInterpreterOptions.UpdateInterpreter();
                }
            }
        }
    }
}

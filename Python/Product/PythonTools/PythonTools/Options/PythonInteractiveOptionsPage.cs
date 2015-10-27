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
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    class PythonInteractiveOptionsPage : PythonDialogPage {
        internal PythonInteractiveOptionsControl _window;

        public PythonInteractiveOptionsPage()
            : base("Interactive Windows") {
        }

        internal static IPythonInterpreterFactory NextOptionsSelection { get; set; }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonInteractiveOptionsControl(Site);
                    PyService.EnvironmentsChanged += PyService_InteractiveWindowsChanged;
                }
                return _window;
            }
        }

        protected override void Dispose(bool disposing) {
            var service = PyService;
            if (service != null) {
                service.EnvironmentsChanged -= PyService_InteractiveWindowsChanged;
            }
            base.Dispose(disposing);
        }

        void PyService_InteractiveWindowsChanged(object sender, EventArgs e) {
            if (_window != null) {
                _window.UpdateInterpreters();
            }
        }

        public PythonInteractiveOptions GetOptions(IPythonInterpreterFactory interpreterFactory) {
            return PyService.GetInteractiveOptions(interpreterFactory);
        }

        public override void ResetSettings() {
            foreach (var options in PyService.InteractiveOptions) {
                options.Value.Reset();
            }
        }

        public override void LoadSettingsFromStorage() {
            var interpreterService = ComponentModel.GetService<IInterpreterOptionsService>();

            var seenIds = new HashSet<Guid>();
            var placeholders = PyService.InteractiveOptions.Where(kv => kv.Key is InterpreterPlaceholder).ToArray();
            PyService.ClearInteractiveOptions();
            foreach (var interpreter in interpreterService.Interpreters) {
                seenIds.Add(interpreter.Id);
                PyService.GetInteractiveOptions(interpreter);
            }

            foreach (var kv in placeholders) {
                if (!seenIds.Contains(kv.Key.Id)) {
                    PyService.AddInteractiveOptions(kv.Key, kv.Value);
                }
            }

            if (_window != null) {
                _window.UpdateInterpreters();
            }
        }

        private PythonInteractiveOptions ReadOptions(IPythonInterpreterFactory interpreter) {
            return PyService.GetInteractiveOptions(interpreter);
        }

        public override void SaveSettingsToStorage() {
            var service = ComponentModel.GetService<IInterpreterOptionsService>();

            foreach (var keyValue in PyService.InteractiveOptions) {
                var interpreter = keyValue.Key;

                if (interpreter is InterpreterPlaceholder) {
                    // Placeholders will be saved by the interpreter options page.
                    continue;
                }
                
                SaveOptions(interpreter, keyValue.Value);
            }
        }

        internal void SaveOptions(IPythonInterpreterFactory interpreter, PythonInteractiveOptions options) {
            options.Save(interpreter);
        }
    }
}

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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Stores options related to the interactive window for a single Python interpreter instance.
    /// </summary>
    class PythonInteractiveOptions : PythonInteractiveCommonOptions {
        private readonly IServiceProvider _serviceProvider;
        private bool _enableAttach;
        private string _startupScript, _executionMode, _interperterOptions;

        private const string ExecutionModeSetting = "ExecutionMode";
        private const string InterpreterOptionsSetting = "InterpreterOptions";
        private const string EnableAttachSetting = "EnableAttach";
        private const string StartupScriptSetting = "StartupScript";

        internal PythonInteractiveOptions(IServiceProvider serviceProvider, PythonToolsService pyService, string category, string id)
            : base(pyService, category, id) {
            _serviceProvider = serviceProvider;
        }

        public bool EnableAttach {
            get { return _enableAttach; }
            set { _enableAttach = value; }
        }

        public string StartupScript {
            get { return _startupScript; }
            set { _startupScript = value; }
        }

        public string ExecutionMode {
            get { return _executionMode; }
            set { _executionMode = value; }
        }

        public string InterpreterOptions {
            get { return _interperterOptions; }
            set { _interperterOptions = value; }
        }

        public new void Load() {
            base.Load();
            EnableAttach = _pyService.LoadBool(_id + EnableAttachSetting, _category) ?? false;
            ExecutionMode = _pyService.LoadString(_id + ExecutionModeSetting, _category) ?? "";
            InterpreterOptions = _pyService.LoadString(_id + InterpreterOptionsSetting, _category) ?? "";
            StartupScript = _pyService.LoadString(_id + StartupScriptSetting, _category) ?? "";
        }

        public void Save(IPythonInterpreterFactory interpreter) {
            base.Save();
            _pyService.SaveString(_id + InterpreterOptionsSetting, _category, InterpreterOptions ?? "");
            _pyService.SaveBool(_id + EnableAttachSetting, _category, EnableAttach);
            _pyService.SaveString(_id + ExecutionModeSetting, _category, ExecutionMode ?? "");
            _pyService.SaveString(_id + StartupScriptSetting, _category, StartupScript ?? "");
            var replProvider = _serviceProvider.GetComponentModel().GetService<InteractiveWindowProvider>();
            if (replProvider != null) {
                // propagate changed settings to existing REPL windows
                foreach (var replWindow in replProvider.GetReplWindows()) {
                    PythonReplEvaluator pyEval = replWindow.Evaluator as PythonReplEvaluator;
                    if (EvaluatorUsesThisInterpreter(pyEval, interpreter)) {
                        if (UseInterpreterPrompts) {
                            replWindow.UseInterpreterPrompts();
                        } else {
                            replWindow.SetPrompts(PrimaryPrompt, SecondaryPrompt);
                        }
                        replWindow.SetSmartUpDown(ReplSmartHistory);
                    }
                }
            }
        }

        private static bool EvaluatorUsesThisInterpreter(PythonReplEvaluator pyEval, IPythonInterpreterFactory interpreter) {
            return pyEval != null &&
                pyEval.Interpreter != null &&
                pyEval.Interpreter.Id == interpreter.Id &&
                pyEval.Interpreter.Configuration.Version == interpreter.Configuration.Version;
        }

        public new void Reset() {
            base.Reset();
            ExecutionMode = "";
            InterpreterOptions = "";
            EnableAttach = false;
            StartupScript = "";
        }
    }
}

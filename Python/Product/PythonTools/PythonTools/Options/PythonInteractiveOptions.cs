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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
#if !DEV14_OR_LATER
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.Options {
#if DEV14_OR_LATER
    using IReplWindowProvider = InteractiveWindowProvider;
#endif

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
            var replProvider = _serviceProvider.GetComponentModel().GetService<IReplWindowProvider>();
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
#if !DEV14_OR_LATER
                        replWindow.SetOptionValue(ReplOptions.DisplayPromptInMargin, !InlinePrompts);
#endif
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

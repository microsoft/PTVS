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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Adornments;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplEvaluatorProvider = IInteractiveEngineProvider;
#endif
    
    [Export(typeof(IReplEvaluatorProvider))]
    class PythonReplEvaluatorProvider : IReplEvaluatorProvider {
        readonly IInterpreterOptionsService _interpService;
        readonly IErrorProviderFactory _errorProviderFactory;

        private const string _replGuid = "FAEC7F47-85D8-4899-8D7B-0B855B732CC8";
        internal const string _configurableGuid = "3C4CB167-E213-4377-8909-437139C3C553";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider([Import] IInterpreterOptionsService interpService, [Import] IErrorProviderFactory errorProviderFactory) {
            Debug.Assert(interpService != null);
            Debug.Assert(errorProviderFactory != null);
            _interpService = interpService;
            _errorProviderFactory = errorProviderFactory;
        }

        #region IReplEvaluatorProvider Members

        public IReplEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_replGuid)) {
                var interpreter = _interpService.FindInterpreter(replId.Substring(_replGuid.Length + 1, _replGuid.Length), replId.Substring(_replGuid.Length * 2 + 2));
                if (interpreter != null) {
                    return new PythonReplEvaluator(interpreter, _errorProviderFactory);
                }
            } else if (replId.StartsWith(_configurableGuid)) {
                return CreateConfigurableInterpreter(replId);
            }
            return null;
        }

        /// <summary>
        /// Creates an interpreter which was created programmatically by some plugin
        /// </summary>
        private IReplEvaluator CreateConfigurableInterpreter(string replId) {
            string[] components = replId.Split(new[] { '|' }, 5);
            if (components.Length == 5) {
                string workingDir = components[0];
                string options = components[1];
                string interpreter = components[2];
                string interpreterVersion = components[3];
                // we don't care about the user identifier

                var pyInterpreter = _interpService.FindInterpreter(interpreter, interpreterVersion);
                // TODO: We'll need to look in virtual env's here too
                if (pyInterpreter != null) {
                    PythonReplCreationOptions optionsVal;
                    Enum.TryParse<PythonReplCreationOptions>(options, out optionsVal);
                    var replOptions = new ConfigurablePythonReplOptions(
                        workingDir
                    );

                    if (optionsVal.HasFlag(PythonReplCreationOptions.DontPersist)) {
                        return new PythonReplEvaluatorDontPersist(pyInterpreter, _errorProviderFactory, replOptions);
                    } else {
                        return new PythonReplEvaluator(pyInterpreter, _errorProviderFactory, replOptions);
                    }
                }
            }
            return null;
        }

        #endregion

        internal static string GetReplId(IPythonInterpreterFactory interpreter) {
            return _replGuid + " " +
                interpreter.Id + " " +
                interpreter.Configuration.Version;
        }
    }

}

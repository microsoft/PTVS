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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Adornments;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplEvaluatorProvider = IInteractiveEngineProvider;
#endif
    
    [Export(typeof(IReplEvaluatorProvider))]
    [ReplRole("Execution")]
    [ReplRole("Reset")]
    class PythonReplEvaluatorProvider : IReplEvaluatorProvider {
        private readonly IPythonInterpreterFactoryProvider[] _interpreters;
        private readonly IErrorProviderFactory _errorProviderFactory;
        private const string _replGuid = "FAEC7F47-85D8-4899-8D7B-0B855B732CC8";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider([ImportMany]IPythonInterpreterFactoryProvider[] interpreters, IErrorProviderFactory errorProviderFactory) {
            _interpreters = interpreters;
            _errorProviderFactory = errorProviderFactory;
        }

        #region IReplEvaluatorProvider Members

        public IReplEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_replGuid)) {
                Guid interpreterGuid = Guid.Parse(replId.Substring(_replGuid.Length + 1, _replGuid.Length));
                Version version = Version.Parse(replId.Substring(_replGuid.Length * 2 + 2));
                foreach (var interpreter in _interpreters) {
                    foreach (var factory in interpreter.GetInterpreterFactories()) {
                        if (factory.Id == interpreterGuid && version == factory.Configuration.Version) {
                            return new PythonReplEvaluator(interpreter, interpreterGuid, version, _errorProviderFactory);
                        }
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

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
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;
#if DEV14_OR_LATER
using IReplEvaluator = Microsoft.VisualStudio.InteractiveWindow.IInteractiveEvaluator;
using IReplEvaluatorProvider = Microsoft.PythonTools.Repl.IInteractiveEvaluatorProvider;
#endif

namespace Microsoft.PythonTools.Repl {

    [Export(typeof(IReplEvaluatorProvider))]
    class PythonDebugReplEvaluatorProvider : IReplEvaluatorProvider {
        private const string _debugReplGuid = "BA417560-5A78-46F1-B065-638D27E1CDD0";
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonDebugReplEvaluatorProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        #region IReplEvaluatorProvider Members

        public IReplEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_debugReplGuid)) {
                return new PythonDebugReplEvaluator(_serviceProvider);
            }
            return null;
        }

        #endregion

        internal static string GetDebugReplId() {
            return _debugReplGuid;
        }
    }
}

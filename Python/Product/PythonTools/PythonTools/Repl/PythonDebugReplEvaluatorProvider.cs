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
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveEvaluatorProvider))]
    class PythonDebugReplEvaluatorProvider : IInteractiveEvaluatorProvider {
        private const string _debugReplGuid = "BA417560-5A78-46F1-B065-638D27E1CDD0";
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonDebugReplEvaluatorProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        public IInteractiveEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_debugReplGuid)) {
                return new PythonDebugReplEvaluator(_serviceProvider);
            }
            return null;
        }

        internal static string GetDebugReplId() {
            return _debugReplGuid;
        }
    }
}

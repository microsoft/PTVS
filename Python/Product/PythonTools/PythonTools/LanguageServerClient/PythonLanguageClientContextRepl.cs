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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class PythonLanguageClientContextRepl : IPythonLanguageClientContext {
        private readonly PythonCommonInteractiveEvaluator _evaluator;

        public event EventHandler InterpreterChanged { add { } remove { } }
        public event EventHandler SearchPathsChanged { add { } remove { } }
        public event EventHandler Closed { add { } remove { } }
        public event EventHandler ReanalyzeProjectChanged { add { } remove { } }


        public PythonLanguageClientContextRepl(PythonCommonInteractiveEvaluator evaluator) {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public InterpreterConfiguration InterpreterConfiguration  => _evaluator.Configuration.Interpreter;

        public string RootPath => null;

        public IEnumerable<string> SearchPaths => _evaluator.Configuration.SearchPaths;

        public void Dispose() { }
    }
}

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientContextRepl : IPythonLanguageClientContext {
        private readonly PythonCommonInteractiveEvaluator _evaluator;
        private readonly string _contentTypeName;

#pragma warning disable CS0067
        public event EventHandler InterpreterChanged;
        public event EventHandler SearchPathsChanged;
        public event EventHandler Closed;
#pragma warning restore CS0067

        public PythonLanguageClientContextRepl(
            PythonCommonInteractiveEvaluator evaluator,
            string contentTypeName
        ) {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _contentTypeName = contentTypeName ?? throw new ArgumentNullException(nameof(contentTypeName));
        }

        public string ContentTypeName => _contentTypeName;

        public InterpreterConfiguration InterpreterConfiguration  => _evaluator.Configuration.Interpreter;

        public string RootPath => null;

        public IEnumerable<string> SearchPaths => _evaluator.Configuration.SearchPaths;

        public object Clone() {
            return new PythonLanguageClientContextRepl(
                _evaluator,
                ContentTypeName
            );
        }
    }
}

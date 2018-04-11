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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace TestUtilities.Python {
    public class MockPythonProjectEntry : IPythonProjectEntry {
        public PythonAst Tree {
            get;
            set;
        }

        public string ModuleName {
            get;
            set;
        }

        public ModuleAnalysis Analysis {
            get;
            set;
        }

        public event EventHandler<EventArgs> OnNewParseTree { add { } remove { } }

        public event EventHandler<EventArgs> OnNewAnalysis { add { } remove { } }

        public void Analyze(System.Threading.CancellationToken cancel, bool enqueueOnly) {
            throw new NotImplementedException();
        }

        public IGroupableAnalysisProject AnalysisGroup {
            get;
            set;
        }

        public bool IsAnalyzed {
            get;
            set;
        }

        public int AnalysisVersion {
            get;
            set;
        }

        public string FilePath {
            get;
            set;
        }

        public Uri DocumentUri {
            get;
            set;
        }

        public string GetLine(int lineNo) {
            throw new NotImplementedException();
        }

        public Dictionary<object, object> Properties {
            get;
            set;
        }

        public void RemovedFromProject() {
            throw new NotImplementedException();
        }

        public IModuleContext AnalysisContext {
            get;
            set;
        }

        public void Analyze(CancellationToken cancel) {
            throw new NotImplementedException();
        }

        public IPythonParse BeginParse() {
            throw new NotImplementedException();
        }

        public IPythonParse GetCurrentParse() {
            throw new NotImplementedException();
        }

        public IPythonParse WaitForCurrentParse(int timeout = Timeout.Infinite, CancellationToken token = default(CancellationToken)) {
            throw new NotImplementedException();
        }
    }
}

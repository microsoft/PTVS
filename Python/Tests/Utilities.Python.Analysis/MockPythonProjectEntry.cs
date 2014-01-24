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

        public void BeginParsingTree() {
            throw new NotImplementedException();
        }

        public void UpdateTree(PythonAst ast, IAnalysisCookie fileCookie) {
            throw new NotImplementedException();
        }

        public void GetTreeAndCookie(out PythonAst ast, out IAnalysisCookie cookie) {
            throw new NotImplementedException();
        }

        public PythonAst WaitForCurrentTree(int timeout = -1) {
            throw new NotImplementedException();
        }

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
    }
}

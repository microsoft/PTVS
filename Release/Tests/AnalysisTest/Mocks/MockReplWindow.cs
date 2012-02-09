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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace AnalysisTest.Mocks {

    class MockReplWindow : IReplWindow {
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _error = new StringBuilder();
        private readonly IReplEvaluator _eval;
        private readonly IWpfTextView _view;

        public MockReplWindow(IReplEvaluator eval) {
            _eval = eval;
            _view = new MockTextView(new MockTextBuffer(""));
        }

        public string Output {
            get {
                return _output.ToString();
            }
        }

        #region IReplWindow Members

        public IWpfTextView TextView {
            get { return _view; }
        }

        public ITextBuffer CurrentLanguageBuffer {
            get { return _view.TextBuffer; }
        }

        public IReplEvaluator Evaluator {
            get { return _eval; }
        }

        public string Title {
            get { return "Mock Repl Window";  }
        }

        public void ClearScreen() {
            _output.Clear();
        }

        public void ClearHistory() {
            throw new NotImplementedException();
        }

        public void Focus() {
            throw new NotImplementedException();
        }

        public void Cancel() {
            throw new NotImplementedException();
        }

        public void InsertCode(string text) {
            throw new NotImplementedException();
        }

        public void Submit(IEnumerable<string> inputs) {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<ExecutionResult> Reset() {
            return ExecutionResult.Succeeded;
        }

        public void AbortCommand() {
        }

        public void WriteLine(string text) {
            _output.AppendLine(text);
        }

        public void WriteOutput(object value) {
            _output.Append(value.ToString());
        }

        public void WriteError(object value) {
            _error.Append(value);
        }

        public string ReadStandardInput() {
            throw new NotImplementedException();
        }

        public void SetOptionValue(ReplOptions option, object value) {
        }

        public object GetOptionValue(ReplOptions option) {
            return null;
        }

        public event Action ReadyForInput {
			add { }
			remove { }
        }

        #endregion
    }
}

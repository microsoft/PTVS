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
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Refactoring {
    using VisualStudio.Text.Editor.OptionsExtensionMethods;
    using AP = AnalysisProtocol;

    class CodeFormatter {
        private readonly ITextView _view;
        private readonly CodeFormattingOptions _format;
        private readonly IServiceProvider _serviceProvider;

        public CodeFormatter(IServiceProvider serviceProvider, ITextView view, CodeFormattingOptions format) {
            _view = view;
            _format = format;
            _serviceProvider = serviceProvider;
        }

        public async void FormatCode(SnapshotSpan span, bool selectResult) {
            var snapshot = _view.TextBuffer.CurrentSnapshot;

            var analyzer = _view.GetAnalyzer(_serviceProvider);

            var res = await analyzer.FormatCode(span, _view.Options.GetNewLineCharacter(), _format);
            AP.ChangeInfo[] changes = res.changes;

            /*
            var walker = new EnclosingNodeWalker(ast, span.Start, span.End);
            ast.Walk(walker);

            if (walker.Target == null ||
                !walker.Target.IsValidSelection || 
                (walker.Target is SuiteTarget && _view.Selection.IsEmpty && selectResult)) {
                return;
            }
            */
            ITrackingSpan selectionSpan = null;
            if (selectResult) {
                selectionSpan = _view.TextBuffer.CurrentSnapshot.CreateTrackingSpan(
                    Span.FromBounds(res.startIndex, res.endIndex), 
                    SpanTrackingMode.EdgeInclusive
                );
            }

            using (var edit = _view.TextBuffer.CreateEdit()) {
                foreach (var change in changes) {
                    edit.Replace(
                        change.start,
                        change.length,
                        change.newText
                    );
                }
                edit.Apply();
            }

            if (selectResult) {
                _view.Selection.Select(selectionSpan.GetSpan(_view.TextBuffer.CurrentSnapshot), false);
            }
        }
    }
}

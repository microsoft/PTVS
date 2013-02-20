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

using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Refactoring {
    class CodeFormatter {
        private readonly ITextView _view;
        private readonly CodeFormattingOptions _format;

        public CodeFormatter(ITextView view, CodeFormattingOptions format) {
            _view = view;
            _format = format;
        }

        public void FormatCode(SnapshotSpan span, bool selectResult) {
            var snapshot = _view.TextBuffer.CurrentSnapshot;

            var ast = _view.GetAnalyzer().ParseFile(snapshot);

            var walker = new EnclosingNodeWalker(ast, span.Start, span.End);
            ast.Walk(walker);

            var body = walker.Target.GetNode(ast);

            ITrackingSpan selectionSpan = null;
            if (selectResult) {
                selectionSpan = _view.TextBuffer.CurrentSnapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive);
            }

            _view.ReplaceByLines(
                body.ToCodeString(ast, _format),
                Span.FromBounds(walker.Target.Start, walker.Target.End)
            );

            if (selectResult) {
                _view.Selection.Select(selectionSpan.GetSpan(_view.TextBuffer.CurrentSnapshot), false);
            }
        }
    }
}

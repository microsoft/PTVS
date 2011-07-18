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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Language.StandardClassification;

namespace Microsoft.PythonTools.Refactoring {
    class VariableRenamer {
        private readonly ITextView _view;

        public VariableRenamer(ITextView textView) {
            _view = textView;
        }

        public void RenameVariable(IRenameVariableInput input, IVsPreviewChangesService previewChanges) {
            if (IsModuleName(input)) {
                input.CannotRename("Cannot rename a module name");
                return;
            }

            var analysis = _view.GetExpressionAnalysis();
            
            string originalName = null;
            Expression expr = null;
            if (analysis != ExpressionAnalysis.Empty) {
                expr = analysis.GetEvaluatedExpression();
                if (expr is NameExpression) {
                    originalName = ((NameExpression)expr).Name;
                } else if (expr is MemberExpression) {
                    originalName = ((MemberExpression)expr).Name;
                }

                if (originalName != null && _view.Selection.IsActive && !_view.Selection.IsEmpty) {
                    if (_view.Selection.Start.Position < analysis.Span.GetStartPoint(_view.TextBuffer.CurrentSnapshot) ||
                        _view.Selection.End.Position > analysis.Span.GetEndPoint(_view.TextBuffer.CurrentSnapshot)) {
                        originalName = null;
                    }
                }
            }

            if (originalName == null) {
                input.CannotRename("Please select a symbol to be renamed.");
                return;
            }

            bool hasVariables = false;
            foreach (var variable in analysis.Variables) {
                if (variable.Type == VariableType.Definition || variable.Type == VariableType.Reference) {
                    hasVariables = true;
                    break;
                }
            }

            IEnumerable<IAnalysisVariable> variables;
            if (!hasVariables) {
                List<IAnalysisVariable> paramVars = GetKeywordParameters(expr, originalName);

                if (paramVars.Count == 0) {
                    input.CannotRename(string.Format("No information is available for the variable '{0}'.", originalName));
                    return;
                }

                variables = paramVars;
            } else {
                variables = analysis.Variables;

            }

            var info = input.GetRenameInfo(originalName);
            if (info != null) {
                var engine = new PreviewChangesEngine(input, analysis, info, originalName, _view.GetAnalyzer(), variables);
                if (info.Preview) {
                    previewChanges.PreviewChanges(engine);
                } else {
                    ErrorHandler.ThrowOnFailure(engine.ApplyChanges());
                }
            }
        }

        private List<IAnalysisVariable> GetKeywordParameters(Expression expr, string originalName) {
            List<IAnalysisVariable> paramVars = new List<IAnalysisVariable>();
            if (expr is NameExpression) {
                // let's check if we'r re-naming a keyword argument...
                ITrackingSpan span = _view.GetCaretSpan();
                var sigs = _view.TextBuffer.CurrentSnapshot.GetSignatures(span);

                foreach (var sig in sigs.Signatures) {
                    IOverloadResult overloadRes = sig as IOverloadResult;
                    if (overloadRes != null) {
                        foreach (var param in overloadRes.Parameters) {
                            if (param.Name == originalName && param.Variables != null) {
                                paramVars.AddRange(param.Variables);
                            }
                        }
                    }
                }
            }
            return paramVars;
        }

        private bool IsModuleName(IRenameVariableInput input) {
            // make sure we're in 
            var span = _view.GetCaretSpan();
            var buffer = span.TextBuffer;
            var snapshot = buffer.CurrentSnapshot;
            var classifier = buffer.GetPythonClassifier();

            bool sawImport = false, sawFrom = false, sawName = false;
            var walker = ReverseExpressionParser.ReverseClassificationSpanEnumerator(classifier, span.GetEndPoint(snapshot));
            while (walker.MoveNext()) {
                var current = walker.Current;
                if (current == null) {
                    // new-line
                    break;
                }

                var text = current.Span.GetText();
                if (current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                    // identifiers are ok
                    sawName = true;
                } else if (current.ClassificationType == classifier.Provider.DotClassification ||
                    current.ClassificationType == classifier.Provider.CommaClassification) {
                    // dots and commas are ok
                } else if (current.ClassificationType == classifier.Provider.GroupingClassification) {
                    if (text != "(" && text != ")") {
                        // list/dict groupings are not ok
                        break;
                    }
                } else if (current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword)) {
                    if (text == "import") {
                        sawImport = true;
                    } else if (text == "from") {
                        sawFrom = true;
                        break;
                    } else if (text == "as") {
                        if (sawName) {
                            // import foo as bar
                            // from foo import bar as baz
                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    break;
                }
            }

            // we saw from, but not import, so we're renaming a module name (from foo, renaming foo)
            // or we saw import, but not a from, so we're renaming a module name
            return sawFrom != sawImport;
        }
    }
}

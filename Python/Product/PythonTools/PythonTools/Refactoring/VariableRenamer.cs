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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Refactoring {
    class VariableRenamer {
        private readonly ITextView _view;
        private readonly IServiceProvider _serviceProvider;

        public VariableRenamer(ITextView textView, IServiceProvider serviceProvider) {
            _view = textView;
            _serviceProvider = serviceProvider;
        }

        public async void RenameVariable(IRenameVariableInput input, IVsPreviewChangesService previewChanges) {
            if (IsModuleName(input)) {
                input.CannotRename("Cannot rename a module name");
                return;
            }

            var caret = _view.GetCaretPosition();
            var analysis = await VsProjectAnalyzer.AnalyzeExpression(
                _view.TextBuffer.CurrentSnapshot,
                caret.Value
            );
            
            string originalName = null;
            string privatePrefix = null;
            Expression expr = null;
            if (!String.IsNullOrWhiteSpace(analysis.Text)) {
                originalName = analysis.MemberName;

                if (analysis.PrivatePrefix != null && originalName != null && originalName.StartsWith("_" + analysis.PrivatePrefix)) {
                    originalName = originalName.Substring(analysis.PrivatePrefix.Length + 1);
                    privatePrefix = analysis.PrivatePrefix;
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
            foreach (var variable in analysis.References) {
                if (variable.Type == VariableType.Definition || variable.Type == VariableType.Reference) {
                    hasVariables = true;
                    break;
                }
            }

            IEnumerable<AnalysisVariable> variables;
            if (!hasVariables) {
                List<AnalysisVariable> paramVars = GetKeywordParameters(expr, originalName);

                if (paramVars.Count == 0) {
                    input.CannotRename(string.Format("No information is available for the variable '{0}'.", originalName));
                    return;
                }

                variables = paramVars;
            } else {
                variables = analysis.References;

            }

            PythonLanguageVersion languageVersion = PythonLanguageVersion.None;
            var analyzer = _view.GetAnalyzer(_serviceProvider);
            var factory = analyzer != null ? analyzer.InterpreterFactory : null;
            if (factory != null) {
                languageVersion = factory.Configuration.Version.ToLanguageVersion();
            }

            var info = input.GetRenameInfo(originalName, languageVersion);
            if (info != null) {
                var engine = new PreviewChangesEngine(_serviceProvider, input, analysis.Text, info, originalName, privatePrefix, _view.GetAnalyzer(_serviceProvider), variables);
                if (info.Preview) {
                    previewChanges.PreviewChanges(engine);
                } else {
                    ErrorHandler.ThrowOnFailure(engine.ApplyChanges());
                }
            }
        }

        private List<AnalysisVariable> GetKeywordParameters(Expression expr, string originalName) {
            List<AnalysisVariable> paramVars = new List<AnalysisVariable>();
            if (expr is NameExpression) {
                // let's check if we'r re-naming a keyword argument...
                ITrackingSpan span = _view.GetCaretSpan();
                var sigs = _view.TextBuffer.CurrentSnapshot.GetSignatures(_serviceProvider, span);

                foreach (var sig in sigs.Signatures) {
                    PythonSignature overloadRes = sig as PythonSignature;
                    if (overloadRes != null) {
                        foreach (PythonParameter param in overloadRes.Parameters) {
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
                            // import fob as oar
                            // from fob import oar as baz
                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    break;
                }
            }

            // we saw from, but not import, so we're renaming a module name (from fob, renaming fob)
            // or we saw import, but not a from, so we're renaming a module name
            return sawFrom != sawImport;
        }
    }
}

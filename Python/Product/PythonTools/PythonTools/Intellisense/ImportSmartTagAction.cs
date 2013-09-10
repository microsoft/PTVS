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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the smart tag action for adding missing import statements.
    /// </summary>
    class ImportSmartTagAction : SmartTagAction {
        public readonly string Name, FromName;
        private readonly ITextBuffer _buffer;
        private readonly ITextView _view;

        /// <summary>
        /// Creates a new smart tag action for an "import foo" smart tag.
        /// </summary>
        public ImportSmartTagAction(string name, ITextBuffer buffer, ITextView view)
            : base(RefactoringIconKind.AddUsing) {
            Name = name;
            _buffer = buffer;
            _view = view;
        }

        /// <summary>
        /// Creates a new smart tag action for a "from foo import bar" smart tag.
        /// </summary>
        public ImportSmartTagAction(string fromName, string name, ITextBuffer buffer, ITextView view)
            : base(RefactoringIconKind.AddUsing) {
            FromName = fromName;
            Name = name;
            _buffer = buffer;
            _view = view;
        }

        public override void Invoke() {
            var analyzer = _buffer.GetAnalyzer();
            var curAst = analyzer.ParseFile(_buffer.CurrentSnapshot);

            SuiteStatement suiteBody = curAst.Body as SuiteStatement;
            Statement insertBefore = null;
            if (suiteBody != null) {
                bool firstStatement = true;

                foreach (var statement in suiteBody.Statements) {
                    if (firstStatement && IsDocString(statement as ExpressionStatement)) {
                        // doc string, ignore this
                        firstStatement = false;
                        continue;
                    }

                    firstStatement = false;

                    // __future__ imports go first
                    if (FromName == null || FromName != "__future__") {
                        if (statement is ImportStatement) {
                            // we insert after this
                            continue;
                        } else if (statement is FromImportStatement) {
                            // we might update this, we might insert after
                            FromImportStatement fromImport = statement as FromImportStatement;
                            if (fromImport.Root.MakeString() == FromName) {
                                // update the existing from ... import statement to include the new name.
                                UpdateFromImport(curAst, fromImport);
                                return;
                            }
                            continue;
                        }
                    }

                    // this isn't an import, we insert before this statement
                    insertBefore = statement;
                    break;
                }
            }

            int start;
            if (insertBefore != null) {
                var location = insertBefore.GetStart(curAst);

                var point = _buffer.CurrentSnapshot.GetLineFromLineNumber(location.Line - 1).Start;
                // the span starts after any whitespace, so walk backup and see if we should skip some lines
                if (point.Position != 0) {
                    var prevPoint = point.Subtract(1);

                    //  walk past all the previous lines
                    var classifier = _buffer.GetPythonClassifier();

                    bool moved = false;
                    while (prevPoint.Position != 0 &&
                        (Char.IsWhiteSpace(prevPoint.GetChar()) || IsCommentChar(prevPoint, classifier))) {
                        prevPoint = prevPoint.Subtract(1);
                        moved = true;
                    }
                    prevPoint = prevPoint.Add(1);

                    // then walk forward one line
                    if (moved) {
                        int lineNum = prevPoint.GetContainingLine().LineNumber;
                        do {
                            prevPoint = prevPoint.Add(1);
                        } while (lineNum == prevPoint.GetContainingLine().LineNumber);
                    }

                    point = prevPoint;
                }
                start = point.Position;
            } else {
                start = 0;
            }

            _buffer.Insert(start, InsertionText + _view.Options.GetNewLineCharacter());
        }

        private static bool IsCommentChar(SnapshotPoint prevPoint, PythonClassifier classifier) {
            IList<ClassificationSpan> spans;
            spans = classifier.GetClassificationSpans(new SnapshotSpan(prevPoint, 1));
            if (spans.Count == 1 && spans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment)) {
                return true;
            }
            return false;
        }

        private static bool IsDocString(ExpressionStatement exprStmt) {
            ConstantExpression constExpr;
            return exprStmt != null &&
                    (constExpr = exprStmt.Expression as ConstantExpression) != null &&
                    (constExpr.Value is string || constExpr.Value is AsciiString);
        }

        private void UpdateFromImport(PythonAst curAst, FromImportStatement fromImport) {
            NameExpression[] names = new NameExpression[fromImport.Names.Count + 1];
            NameExpression[] asNames = fromImport.AsNames == null ? null : new NameExpression[fromImport.AsNames.Count + 1];
            NameExpression newName = new NameExpression(Name);
            for (int i = 0; i < fromImport.Names.Count; i++) {
                names[i] = fromImport.Names[i];
            }
            names[fromImport.Names.Count] = newName;

            if (asNames != null) {
                for (int i = 0; i < fromImport.AsNames.Count; i++) {
                    asNames[i] = fromImport.AsNames[i];
                }
            }

            var newImport = new FromImportStatement((ModuleName)fromImport.Root, names, asNames, fromImport.IsFromFuture, fromImport.ForceAbsolute);
            curAst.CopyAttributes(fromImport, newImport);

            var newCode = newImport.ToCodeString(curAst);

            var span = fromImport.GetSpan(curAst);
            int leadingWhiteSpaceLength = (fromImport.GetLeadingWhiteSpace(curAst) ?? "").Length;
            using (var edit = _buffer.CreateEdit()) {
                edit.Delete(span.Start.Index - leadingWhiteSpaceLength, span.Length + leadingWhiteSpaceLength);
                edit.Insert(span.Start.Index, newCode);
                edit.Apply();
            }
        }

        public override string DisplayText {
            get {
                return InsertionText.Replace("_", "__");
            }
        }

        private string InsertionText {
            get {
                string res;
                if (FromName != null) {
                    res = "from " + FromName + " import " + Name;
                } else {
                    res = "import " + Name;
                }
                return res;
            }
        }
    }
}

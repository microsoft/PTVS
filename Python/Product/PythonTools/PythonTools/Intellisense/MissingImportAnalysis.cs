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
using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Linq;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides information about names which are missing import statements but the
    /// name refers to an identifier in another module.
    /// 
    /// New in 1.1.
    /// </summary>
    public sealed class MissingImportAnalysis {
        internal static MissingImportAnalysis Empty = new MissingImportAnalysis();
        private readonly ITrackingSpan _span;
        private readonly string _name;
        private readonly PythonAnalyzer _state;
        private IEnumerable<ExportedMemberInfo> _imports;

        private MissingImportAnalysis() {
            _imports = new ExportedMemberInfo[0];
        }

        internal MissingImportAnalysis(string name, PythonAnalyzer state, ITrackingSpan span) {
            _span = span;
            _name = name;
            _state = state;
        }

        /// <summary>
        /// The locations this name can be imported from.  The names are fully qualified with
        /// the module/package names and the name its self.  For example for "fob" defined in the "oar"
        ///  module the name here is oar.fob.  This list is lazily calculated (including loading of cached intellisense data) 
        ///  so that you can break from the enumeration early and save significant work.
        /// </summary>
        public IEnumerable<ExportedMemberInfo> AvailableImports {
            get {
                if (_imports == null) {
                    _imports = _state.FindNameInAllModules(_name);
                }
                return _imports;
            }
        }

        /// <summary>
        /// The locations this name can be imported from.  The names are fully qualified with
        /// the module/package names and the name its self.  For example for "fob" defined in the "oar"
        ///  module the name here is oar.fob.  This list is lazily calculated (including loading of cached intellisense data) 
        ///  so that you can break from the enumeration early and save significant work.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public async Task<IEnumerable<ExportedMemberInfo>> GetAvailableImportsAsync(CancellationToken cancellationToken) {
            if (_imports != null) {
                return _imports;
            }
            var imports = await Task.Run(() => {
                var r = new List<ExportedMemberInfo>();
                foreach (var i in _state.FindNameInAllModules(_name)) {
                    r.Add(i);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return r;
            });
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.CompareExchange(ref _imports, imports, null) ?? imports;
        }

        /// <summary>
        /// The span which covers the identifier used to trigger this missing import analysis.
        /// </summary>
        public ITrackingSpan ApplicableToSpan {
            get {
                return _span;
            }
        }

        public static string MakeImportCode(string fromModule, string name) {
            if (string.IsNullOrEmpty(fromModule)) {
                return string.Format("import {0}", name);
            } else {
                return string.Format("from {0} import {1}", fromModule, name);
            }
        }

        public static void AddImport(
            IServiceProvider serviceProvider,
            ITextBuffer buffer,
            ITextView view,
            string fromModule,
            string name
        ) {
            var analyzer = buffer.GetAnalyzer(serviceProvider);
            var curAst = analyzer.ParseSnapshot(buffer.CurrentSnapshot);

            var suiteBody = curAst.Body as SuiteStatement;
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
                    if (fromModule == null || fromModule != "__future__") {
                        if (statement is ImportStatement) {
                            // we insert after this
                            continue;
                        } else if (statement is FromImportStatement) {
                            // we might update this, we might insert after
                            FromImportStatement fromImport = statement as FromImportStatement;
                            if (fromImport.Root.MakeString() == fromModule) {
                                // update the existing from ... import statement to include the new name.
                                UpdateFromImport(curAst, buffer, fromImport, name);
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

                var point = buffer.CurrentSnapshot.GetLineFromLineNumber(location.Line - 1).Start;
                // the span starts after any whitespace, so walk backup and see if we should skip some lines
                if (point.Position != 0) {
                    var prevPoint = point.Subtract(1);

                    //  walk past all the previous lines
                    var classifier = buffer.GetPythonClassifier();

                    bool moved = false;
                    while (prevPoint.Position != 0 &&
                        (char.IsWhiteSpace(prevPoint.GetChar()) || IsCommentChar(prevPoint, classifier))) {
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

            buffer.Insert(start, MakeImportCode(fromModule, name) + view.Options.GetNewLineCharacter());
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

        private static void UpdateFromImport(
            PythonAst curAst,
            ITextBuffer buffer,
            FromImportStatement fromImport,
            string name
        ) {
            NameExpression[] names = new NameExpression[fromImport.Names.Count + 1];
            NameExpression[] asNames = fromImport.AsNames == null ? null : new NameExpression[fromImport.AsNames.Count + 1];
            NameExpression newName = new NameExpression(name);
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
            using (var edit = buffer.CreateEdit()) {
                edit.Delete(span.Start.Index - leadingWhiteSpaceLength, span.Length + leadingWhiteSpaceLength);
                edit.Insert(span.Start.Index, newCode);
                edit.Apply();
            }
        }

    }
}

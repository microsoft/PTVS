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
using Microsoft.PythonTools.Django.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class TemplateClassifier : IClassifier {
        private readonly ITextBuffer _textBuffer;
        private readonly TemplateClassifierProvider _classifierProvider;

        public TemplateClassifier(TemplateClassifierProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _classifierProvider = provider;
        }

        #region IClassifier Members

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged {
            add { }
            remove { }
        }

        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span) {
            TemplateTokenKind kind;
            List<ClassificationSpan> spans = new List<ClassificationSpan>();
            spans.Add(
                new ClassificationSpan(
                    new SnapshotSpan(
                        span.Snapshot,
                        new Span(0, 2)
                    ),
                    _classifierProvider._templateClassType
                )
            );

            if (_textBuffer.Properties.TryGetProperty<TemplateTokenKind>(typeof(TemplateTokenKind), out kind)) {
                switch (kind) {
                    case TemplateTokenKind.Comment:
                        spans.Add(
                            new ClassificationSpan(
                                new SnapshotSpan(
                                    span.Snapshot,
                                    Span.FromBounds(2, span.Snapshot.Length - 2)
                                ),
                                _classifierProvider._commentClassType
                            )
                        );
                        break;
                    case TemplateTokenKind.Variable:
                        ClassifyVariable(span, spans);
                        break;
                    case TemplateTokenKind.Block:
                        spans.Add(
                            new ClassificationSpan(
                                new SnapshotSpan(
                                    span.Snapshot,
                                    Span.FromBounds(2, span.Snapshot.Length - 2)
                                ),
                                _classifierProvider._classType
                            )
                        );
                        break;
                }
            }

            spans.Add(
                new ClassificationSpan(
                    new SnapshotSpan(
                        span.Snapshot,
                        new Span(span.Snapshot.Length - 2, 2)
                    ),
                    _classifierProvider._templateClassType
                )
            );

            return spans;
        }

        private void ClassifyVariable(SnapshotSpan span, List<ClassificationSpan> spans) {
            string filterText;
            int filterStart;
            filterText = GetTrimmedFilterText(span, out filterStart);
            if (filterText == null) {
                return;
            }

            var filterInfo = DjangoVariable.Parse(filterText);

            AddVariableClassifications(span.Snapshot, spans, filterInfo.Expression, filterStart + filterInfo.ExpressionStart);

            for (int i = 0; i < filterInfo.Filters.Length; i++) {
                var curFilter = filterInfo.Filters[i];

                spans.Add(
                    new ClassificationSpan(
                        new SnapshotSpan(
                            span.Snapshot,
                            new Span(filterStart + curFilter.FilterStart, curFilter.Filter.Length)
                        ),
                        _classifierProvider._identifierType
                    )
                );
                
                AddVariableClassifications(span.Snapshot, spans, curFilter.Arg, filterStart + curFilter.ArgStart);                
            }
        }

        private void AddVariableClassifications(ITextSnapshot snapshot, List<ClassificationSpan> spans, DjangoVariableValue expr, int start) {
            if (expr != null) {
                IClassificationType filterType;
                switch (expr.Kind) {
                    case DjangoVariableKind.Constant: filterType = _classifierProvider._literalType; break;
                    case DjangoVariableKind.Number: filterType = _classifierProvider._numberType; break;
                    case DjangoVariableKind.Variable:
                        // variable can have dots in it...
                        if (expr.Value.IndexOf('.') != -1) {
                            AddDottedIdentifierClassifications(snapshot, spans, start, expr);
                            return;
                        }
                        filterType = _classifierProvider._identifierType;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                spans.Add(
                    new ClassificationSpan(
                        new SnapshotSpan(
                            snapshot,
                            new Span(start, expr.Value.Length)
                        ),
                        filterType
                    )
                );
            }
        }

        private void AddDottedIdentifierClassifications(ITextSnapshot snapshot, List<ClassificationSpan> spans, int start, DjangoVariableValue expr) {
            var split = expr.Value.Split('.');
            for (int i = 0; i < split.Length; i++) {
                spans.Add(
                    new ClassificationSpan(
                        new SnapshotSpan(
                            snapshot,
                            new Span(start, split[i].Length)
                        ),
                        _classifierProvider._identifierType
                    )
                );
                start += split[i].Length;
                spans.Add(
                    new ClassificationSpan(
                        new SnapshotSpan(
                            snapshot,
                            new Span(start, 1)
                        ),
                        _classifierProvider._dot
                    )
                );
                start += 1;
            }
        }

        /// <summary>
        /// Gets the trimmed filter text and passes back the position in the buffer where the first
        /// character of the filter actually starts.
        private static string GetTrimmedFilterText(SnapshotSpan span, out int start) {
            start = 0;

            string filterText = null;
            int? tmpStart = null;
            for (int i = 2; i < span.Snapshot.Length; i++) {
                if (!Char.IsWhiteSpace(span.Snapshot[i])) {
                    tmpStart = start = i;
                    break;
                }
            }
            if (tmpStart != null) {
                for (int i = span.Snapshot.Length - 3; i > tmpStart.Value; i--) {
                    if (!Char.IsWhiteSpace(span.Snapshot[i])) {
                        filterText = span.Snapshot.GetText(Span.FromBounds(tmpStart.Value, i + 1));

                        break;
                    }
                }
            }

            return filterText;
        }

        #endregion
    }
}

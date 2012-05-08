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
            List<ClassificationSpan> spans = new List<ClassificationSpan>();

            TemplateProjectionBuffer projBuffer;
            if (_textBuffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer)) {
                foreach (var region in projBuffer.GetTemplateRegions(span)) {
                    // classify {{, {#, or {%
                    spans.Add(
                        new ClassificationSpan(
                            new SnapshotSpan(
                                span.Snapshot,
                                new Span(region.Start, 2)
                            ),
                            _classifierProvider._templateClassType
                        )
                    );
                    
                    // classify template tag body
                    ClassifyTemplateBody(span.Snapshot, spans, region);
                    
                    // classify }}, #}, or %}
                    spans.Add(
                        new ClassificationSpan(
                            new SnapshotSpan(
                                span.Snapshot,
                                new Span(region.Start + region.Text.Length - 2, 2)
                            ),
                            _classifierProvider._templateClassType
                        )
                    );
                }
            }

            return spans;
        }

        private void ClassifyTemplateBody(ITextSnapshot snapshot, List<ClassificationSpan> spans, TemplateRegion region) {
            switch (region.Kind) {
                case TemplateTokenKind.Comment:
                    spans.Add(
                        new ClassificationSpan(
                            new SnapshotSpan(
                                snapshot,
                                new Span(region.Start + 2, region.Text.Length - 4)
                            ),
                            _classifierProvider._commentClassType
                        )
                    );
                    break;
                case TemplateTokenKind.Variable:
                    ClassifyVariable(snapshot, spans, region);
                    break;
                case TemplateTokenKind.Block:
                    var blockInfo = DjangoBlock.Parse(region.Text);
                    if (blockInfo != null) {
                        foreach (var curSpan in blockInfo.GetSpans()) {
                            spans.Add(
                                new ClassificationSpan(
                                    new SnapshotSpan(
                                        snapshot,
                                        new Span(
                                            curSpan.Span.Start + region.Start,
                                            curSpan.Span.Length
                                        )
                                    ),
                                    GetClassification(curSpan.Classification)
                                )
                            );
                        }
                    } else {
                        spans.Add(
                            new ClassificationSpan(
                                new SnapshotSpan(
                                    snapshot,
                                    new Span(region.Start + 2, region.Text.Length - 4)
                                ),
                                _classifierProvider._classType
                            )
                        );
                    }
                    break;
            }
        }

        private IClassificationType GetClassification(Classification classification) {
            switch (classification) {
                case Classification.None:           return _classifierProvider._classType;                    
                case Classification.Keyword:        return _classifierProvider._keywordType;
                case Classification.ExcludedCode:   return _classifierProvider._excludedCode;
                default: throw new InvalidOperationException();
            }
        }

        private void ClassifyVariable(ITextSnapshot snapshot, List<ClassificationSpan> spans, TemplateRegion region) {
            var filterInfo = DjangoVariable.Parse(region.Text);
            if (filterInfo == null) {
                // TODO: Report error
                return;
            }

            AddVariableClassifications(snapshot, spans, filterInfo.Expression, filterInfo.ExpressionStart + region.Start);

            for (int i = 0; i < filterInfo.Filters.Length; i++) {
                var curFilter = filterInfo.Filters[i];

                spans.Add(
                    new ClassificationSpan(
                        new SnapshotSpan(
                            snapshot,
                            new Span(curFilter.FilterStart + region.Start, curFilter.Filter.Length)
                        ),
                        _classifierProvider._identifierType
                    )
                );

                AddVariableClassifications(snapshot, spans, curFilter.Arg, curFilter.ArgStart + region.Start);
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


        #endregion
    }
}

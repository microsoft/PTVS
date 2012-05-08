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

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

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
                    var filterInfo = DjangoVariable.Parse(region.Text);
                    
                    if (filterInfo != null) {
                        foreach(var curSpan in filterInfo.GetSpans()) {
                            spans.Add(ToClassification(curSpan, snapshot, region));
                        }
                    }
                    break;
                case TemplateTokenKind.Block:
                    var blockInfo = region.Block;
                    if (blockInfo != null) {
                        foreach (var curSpan in blockInfo.GetSpans()) {
                            spans.Add(ToClassification(curSpan, snapshot, region));
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

        private ClassificationSpan ToClassification(BlockClassification curSpan, ITextSnapshot snapshot, TemplateRegion region) {
            return new ClassificationSpan(
                new SnapshotSpan(
                    snapshot,
                    new Span(
                        curSpan.Span.Start + region.Start,
                        curSpan.Span.Length
                    )
                ),
                GetClassification(curSpan.Classification)
            );
        }

        private IClassificationType GetClassification(Classification classification) {
            switch (classification) {
                case Classification.None:           return _classifierProvider._classType;                    
                case Classification.Keyword:        return _classifierProvider._keywordType;
                case Classification.ExcludedCode:   return _classifierProvider._excludedCode;
                case Classification.Identifier:     return _classifierProvider._identifierType;
                case Classification.Dot:            return _classifierProvider._dot;
                case Classification.Literal:        return _classifierProvider._literalType;
                case Classification.Number:         return _classifierProvider._numberType;
                default: throw new InvalidOperationException();
            }
        }

        #endregion

        internal void RaiseClassificationChanged(SnapshotPoint start, SnapshotPoint end) {
            var classChanged = ClassificationChanged;
            if (classChanged != null) {
                classChanged(this, new ClassificationChangedEventArgs(new SnapshotSpan(start, end)));
            }
        }
    }
}

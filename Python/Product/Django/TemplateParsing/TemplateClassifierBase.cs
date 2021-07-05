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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal abstract class TemplateClassifierBase : IClassifier {
        protected readonly ITextBuffer _textBuffer;
        protected readonly TemplateClassifierProviderBase _classifierProvider;

        protected TemplateClassifierBase(TemplateClassifierProviderBase provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _classifierProvider = provider;
        }

        #region IClassifier Members

        public abstract event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public abstract IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span);

        protected void ClassifyTemplateBody(ITextSnapshot snapshot, List<ClassificationSpan> spans, TemplateRegion region, int prefixLength, int suffixLength) {
            switch (region.Kind) {
                case TemplateTokenKind.Comment:
                    spans.Add(
                        new ClassificationSpan(
                            new SnapshotSpan(
                                snapshot,
                                new Span(region.Start + prefixLength, region.Text.Length - (prefixLength + suffixLength))
                            ),
                            _classifierProvider._commentClassType
                        )
                    );
                    break;
                case TemplateTokenKind.Variable:
                    var filterInfo = DjangoVariable.Parse(region.Text);

                    if (filterInfo != null) {
                        foreach (var curSpan in filterInfo.GetSpans()) {
                            spans.Add(ToClassificationSpan(curSpan, snapshot, region.Start));
                        }
                    }
                    break;
                case TemplateTokenKind.Block:
                    var blockInfo = region.Block ?? DjangoBlock.Parse(region.Text);
                    if (blockInfo != null) {
                        foreach (var curSpan in blockInfo.GetSpans()) {
                            spans.Add(ToClassificationSpan(curSpan, snapshot, region.Start));
                        }
                    } else if (region.Text.Length > (prefixLength + suffixLength)) {    // unterminated block at end of file
                        spans.Add(
                            new ClassificationSpan(
                                new SnapshotSpan(
                                    snapshot,
                                    new Span(region.Start + prefixLength, region.Text.Length - (prefixLength + suffixLength))
                                ),
                                _classifierProvider._classType
                            )
                        );
                    }
                    break;
            }
        }

        protected ClassificationSpan ToClassificationSpan(BlockClassification curSpan, ITextSnapshot snapshot, int start) {
            return new ClassificationSpan(
                new SnapshotSpan(
                    snapshot,
                    new Span(
                        curSpan.Span.Start + start,
                        curSpan.Span.Length
                    )
                ),
                GetClassification(curSpan.Classification)
            );
        }

        protected IClassificationType GetClassification(Classification classification) {
            switch (classification) {
                case Classification.None: return _classifierProvider._classType;
                case Classification.Keyword: return _classifierProvider._keywordType;
                case Classification.ExcludedCode: return _classifierProvider._excludedCode;
                case Classification.Identifier: return _classifierProvider._identifierType;
                case Classification.Dot: return _classifierProvider._dot;
                case Classification.Literal: return _classifierProvider._literalType;
                case Classification.Number: return _classifierProvider._numberType;
                default: throw new InvalidOperationException();
            }
        }

        #endregion
    }
}

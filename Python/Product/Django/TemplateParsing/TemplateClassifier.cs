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
    internal class TemplateClassifier : TemplateClassifierBase {
        private HtmlEditorDocument _htmlDoc;
        private int _deferredClassifications;

        public TemplateClassifier(TemplateClassifierProviderBase provider, ITextBuffer textBuffer)
            : base(provider, textBuffer) {
        }

        public override event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        internal static HtmlEditorDocument HtmlEditorDocumentFromTextBuffer(ITextBuffer buffer) {
            var doc = HtmlEditorDocument.TryFromTextBuffer(buffer);
            if (doc == null) {
                var projBuffer = buffer as IProjectionBuffer;
                if (projBuffer != null) {
                    foreach (var b in projBuffer.SourceBuffers) {
                        if (b.ContentType.IsOfType(TemplateHtmlContentType.ContentTypeName) &&
                            (doc = HtmlEditorDocument.TryFromTextBuffer(b)) != null) {
                            return doc;
                        }
                    }
                }
            }
            return doc;
        }

        public override IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var spans = new List<ClassificationSpan>();

            var htmlDoc = HtmlEditorDocumentFromTextBuffer(span.Snapshot.TextBuffer);
            if (htmlDoc == null) {
                return spans;
            }

            if (_htmlDoc == null) {
                _htmlDoc = htmlDoc;
                _htmlDoc.HtmlEditorTree.UpdateCompleted += HtmlEditorTree_UpdateCompleted;
            } else {
                Debug.Assert(htmlDoc == _htmlDoc);
            }

            // If the tree is not up to date with respect to the current snapshot, then artifact ranges and the
            // associated parse results are also not up to date. We cannot force a refresh here because this
            // can potentially change the buffer, which is not legal for GetClassificationSpans to do, and will
            // break the editor. Queue the refresh for later, and asynchronously notify the editor that it needs
            // to re-classify once it's done.
            if (!_htmlDoc.HtmlEditorTree.IsReady) {
                Interlocked.Increment(ref _deferredClassifications);
                return spans;
            }

            // The provided span may be in a projection snapshot, so we need to
            // map back to the source snapshot to find the correct
            // classification. If projSnapshot is null, we are already in the
            // correct snapshot.
            var projSnapshot = span.Snapshot as IProjectionSnapshot;
            var sourceSnapshot = span.Snapshot;

            var sourceStartIndex = span.Start.Position;
            if (projSnapshot != null) {
                var pt = projSnapshot.MapToSourceSnapshot(sourceStartIndex);
                sourceStartIndex = pt.Position;
                sourceSnapshot = pt.Snapshot;
                if (HtmlEditorDocument.TryFromTextBuffer(sourceSnapshot.TextBuffer) != _htmlDoc) {
                    return spans;
                }
            }

            var index = _htmlDoc.HtmlEditorTree.ArtifactCollection.GetItemContaining(sourceStartIndex);
            if (index < 0) {
                return spans;
            }

            var artifact = _htmlDoc.HtmlEditorTree.ArtifactCollection[index] as TemplateArtifact;
            if (artifact == null) {
                return spans;
            }

            int artifactStart = artifact.InnerRange.Start;
            var artifactText = _htmlDoc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange.Start, artifact.InnerRange.Length);
            artifact.Parse(artifactText);

            var classifications = artifact.GetClassifications();
            foreach (var classification in classifications) {
                var cls = GetClassification(classification.Classification);
                int clsStart = artifactStart + classification.Span.Start;
                int clsLen = Math.Min(sourceSnapshot.Length - clsStart, classification.Span.Length);
                var clsSpan = new SnapshotSpan(sourceSnapshot, clsStart, clsLen);
                if (projSnapshot != null) {
                    foreach (var sp in projSnapshot.MapFromSourceSnapshot(clsSpan)) {
                        spans.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, sp), cls));
                    }
                } else {
                    spans.Add(new ClassificationSpan(clsSpan, cls));
                }
            }

            return spans;
        }

        private void HtmlEditorTree_UpdateCompleted(object sender, EventArgs e) {
            if (!_htmlDoc.HtmlEditorTree.IsReady) {
                return;
            }

            int deferredClassifications = Interlocked.Exchange(ref _deferredClassifications, 0);
            if (deferredClassifications > 0) {
                var classificationChanged = ClassificationChanged;
                if (classificationChanged != null) {
                    var snapshot = _textBuffer.CurrentSnapshot;
                    var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
                    classificationChanged(this, new ClassificationChangedEventArgs(span));
                }
            }
        }
    }
}
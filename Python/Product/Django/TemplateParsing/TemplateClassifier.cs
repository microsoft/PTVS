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
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.Html.Editor.Document;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateClassifier : TemplateClassifierBase {
        private HtmlEditorDocument _htmlDoc;
        private int _deferredClassifications;

        public TemplateClassifier(TemplateClassifierProviderBase provider, ITextBuffer textBuffer)
            : base(provider, textBuffer) {
        }

        public override event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public override IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var spans = new List<ClassificationSpan>();

            var htmlDoc = HtmlEditorDocument.TryFromTextBuffer(span.Snapshot.TextBuffer);
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

            var projSnapshot = _htmlDoc.PrimaryView.TextSnapshot as IProjectionSnapshot;
            if (projSnapshot == null) {
                return spans;
            }

            var primarySpans = projSnapshot.MapFromSourceSnapshot(span);
            foreach (var primarySpan in primarySpans) {
                var index = _htmlDoc.HtmlEditorTree.ArtifactCollection.GetItemContaining(primarySpan.Start);
                if (index < 0) {
                    continue;
                }

                var artifact = _htmlDoc.HtmlEditorTree.ArtifactCollection[index] as TemplateArtifact;
                if (artifact == null) {
                    continue;
                }

                var artifactStart = projSnapshot.MapToSourceSnapshot(artifact.InnerRange.Start);
                if (artifactStart.Snapshot != span.Snapshot) {
                    continue;
                }

                var artifactText = _htmlDoc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
                artifact.Parse(artifactText);

                var classifications = artifact.GetClassifications();
                foreach (var classification in classifications) {
                    var classificationSpan = ToClassificationSpan(classification, span.Snapshot, artifactStart.Position);
                    spans.Add(classificationSpan);
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
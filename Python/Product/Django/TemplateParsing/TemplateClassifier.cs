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

#if DEV12_OR_LATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Html.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;

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

#endif
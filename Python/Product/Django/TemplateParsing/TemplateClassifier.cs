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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;

#if DEV14_OR_LATER
using Microsoft.Html.Editor.Document;
#else
using Microsoft.Html.Editor;
#endif

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateClassifier : TemplateClassifierBase {
        private HtmlEditorDocument _htmlDoc;
        private int _deferredClassifications;

        public TemplateClassifier(TemplateClassifierProviderBase provider, ITextBuffer textBuffer)
            : base(provider, textBuffer) {
        }

        public override event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        internal static HtmlEditorDocument HtmlEditorDocumentFromTextBuffer(ITextBuffer buffer) {
            var doc = HtmlEditorDocument.FromTextBuffer(buffer);
#if DEV14_OR_LATER
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
#endif
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
            var artifactText = _htmlDoc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
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

#endif
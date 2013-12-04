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

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateClassifier : TemplateClassifierBase {
        public TemplateClassifier(TemplateClassifierProviderBase provider, ITextBuffer textBuffer)
            : base(provider, textBuffer) {
        }

        public override IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var spans = new List<ClassificationSpan>();

            var doc = Microsoft.Html.Editor.HtmlEditorDocument.TryFromTextBuffer(span.Snapshot.TextBuffer);
            if (doc == null) {
                return spans;
            }
            doc.HtmlEditorTree.EnsureTreeReady();

            var projSnapshot = doc.PrimaryView.TextSnapshot as IProjectionSnapshot;
            if (projSnapshot == null) {
                return spans;
            }

            var primarySpans = projSnapshot.MapFromSourceSnapshot(span);
            foreach (var primarySpan in primarySpans) {
                var index = doc.HtmlEditorTree.ArtifactCollection.GetItemContaining(primarySpan.Start);
                if (index < 0) {
                    continue;
                }

                var artifact = doc.HtmlEditorTree.ArtifactCollection[index] as TemplateArtifact;
                if (artifact == null) {
                    continue;
                }

                var artifactStart = projSnapshot.MapToSourceSnapshot(artifact.InnerRange.Start);
                if (artifactStart.Snapshot != span.Snapshot) {
                    continue;
                }

                var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
                artifact.Parse(artifactText);

                var classifications = artifact.GetClassifications();
                foreach (var classification in classifications) {
                    var classificationSpan = ToClassificationSpan(classification, span.Snapshot, artifactStart.Position);
                    spans.Add(classificationSpan);
                }
            }

            return spans;
        }
    }
}

#endif
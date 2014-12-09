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
using System.Linq;
using Microsoft.Html.Editor;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class DjangoCompletionSource : DjangoCompletionSourceBase {
        public DjangoCompletionSource(IGlyphService glyphService, DjangoAnalyzer analyzer, ITextBuffer textBuffer)
            : base(glyphService, analyzer, textBuffer) {
        }

        public override void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            var doc = HtmlEditorDocument.FromTextBuffer(_buffer);
            if (doc == null) {
                return;
            }
            doc.HtmlEditorTree.EnsureTreeReady();

            var primarySnapshot = doc.PrimaryView.TextSnapshot;
            var nullableTriggerPoint = session.GetTriggerPoint(primarySnapshot);
            if (!nullableTriggerPoint.HasValue) {
                return;
            }
            var triggerPoint = nullableTriggerPoint.Value;

            var artifacts = doc.HtmlEditorTree.ArtifactCollection;
            var index = artifacts.GetItemContaining(triggerPoint.Position);
            if (index < 0) {
                return;
            }

            var artifact = artifacts[index] as TemplateArtifact;
            if (artifact == null) {
                return;
            }

            var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
            artifact.Parse(artifactText);

            ITrackingSpan applicableSpan;
            var completionSet = GetCompletionSet(session.GetOptions(_analyzer._serviceProvider), _analyzer, artifact.TokenKind, artifactText, artifact.InnerRange.Start, triggerPoint, out applicableSpan);
            completionSets.Add(completionSet);
        }

        protected override IEnumerable<DjangoBlock> GetBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint) {
            var doc = HtmlEditorDocument.FromTextBuffer(_buffer);
            if (doc == null) {
                yield break;
            }

            var artifacts = doc.HtmlEditorTree.ArtifactCollection.ItemsInRange(new Microsoft.Web.Core.TextRange(0, triggerPoint.Position));
            foreach (var artifact in artifacts.OfType<TemplateBlockArtifact>().Reverse()) {
                var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
                artifact.Parse(artifactText);
                if (artifact.Block != null) {
                    yield return artifact.Block;
                }
            }
        }
    }
}

#endif
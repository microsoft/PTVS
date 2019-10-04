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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.WebTools.Languages.Html.Editor.Document;
using Microsoft.WebTools.Languages.Shared.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class DjangoCompletionSource : DjangoCompletionSourceBase {
        public DjangoCompletionSource(IGlyphService glyphService, IDjangoProjectAnalyzer analyzer, ITextBuffer textBuffer)
            : base(glyphService, analyzer, textBuffer) {
        }

        public override void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            var doc = TemplateClassifier.HtmlEditorDocumentFromTextBuffer(_buffer);
            if (doc == null) {
                return;
            }
            var tree = doc.HtmlEditorTree;
            if (tree == null) {
                return;
            }
            tree.EnsureTreeReady();

            var primarySnapshot = tree.TextSnapshot;
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

            var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange.Start, artifact.InnerRange.Length);
            artifact.Parse(artifactText);

            var completionSet = GetCompletionSet(_analyzer, artifact.TokenKind, artifactText, artifact.InnerRange.Start, triggerPoint, out _);
            completionSets.Add(completionSet);
        }

        protected override IEnumerable<DjangoBlock> GetBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint) {
            var buffers = _buffer.GetContributingBuffers().Where(b => b.ContentType.IsOfType(TemplateHtmlContentType.ContentTypeName));
            var doc = HtmlEditorDocument.TryFromTextBuffer(buffers.FirstOrDefault() ?? _buffer);
            if (doc == null) {
                yield break;
            }

            var artifacts = doc.HtmlEditorTree.ArtifactCollection.ItemsInRange(new TextRange(0, triggerPoint.Position));
            foreach (var artifact in artifacts.OfType<TemplateBlockArtifact>().Reverse()) {
                var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange.Start, artifact.InnerRange.Length);
                artifact.Parse(artifactText);
                if (artifact.Block != null) {
                    yield return artifact.Block;
                }
            }
        }
    }
}

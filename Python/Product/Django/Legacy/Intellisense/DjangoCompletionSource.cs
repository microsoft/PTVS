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

#if !DEV12_OR_LATER

using System.Linq;
using System.Collections.Generic;
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
            var nullableTriggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            if (!nullableTriggerPoint.HasValue) {
                return;
            }

            var triggerPoint = nullableTriggerPoint.Value;
            TemplateProjectionBuffer projBuffer;
            if (!_buffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer)) {
                return;
            }

            int templateStart;
            TemplateTokenKind kind;
            var templateText = projBuffer.GetTemplateText(triggerPoint, out kind, out templateStart);
            if (templateText == null) {
                return;
            }

            if (kind == TemplateTokenKind.Block || kind == TemplateTokenKind.Variable) {
                ITrackingSpan applicableSpan;
                var completionSet = GetCompletionSet(
                    session.GetOptions(),
                    _analyzer,
                    kind,
                    templateText,
                    templateStart,
                    triggerPoint,
                    out applicableSpan);
                completionSets.Add(completionSet);
            }
        }

        protected override IEnumerable<DjangoBlock> GetBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint) {
            var projBuffer = _buffer.Properties.GetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer));
            var regions = projBuffer.GetTemplateRegions(
                new SnapshotSpan(new SnapshotPoint(triggerPoint.Snapshot, 0), triggerPoint),
                reversed: true
            );
            return from region in regions
                   where region.Kind == TemplateTokenKind.Block && region.Block != null
                   select region.Block;
        }
    }
}

#endif
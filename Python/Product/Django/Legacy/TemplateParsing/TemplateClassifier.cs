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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Django.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateClassifier : TemplateClassifierBase {
        public TemplateClassifier(TemplateClassifierProviderBase provider, ITextBuffer textBuffer)
            : base(provider, textBuffer) {
        }

        public override IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
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
                    ClassifyTemplateBody(span.Snapshot, spans, region, 2, 2);
                    
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
    }
}

#endif
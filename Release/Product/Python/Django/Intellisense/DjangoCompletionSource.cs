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

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using Microsoft.PythonTools.Django.Project;
using System.Linq;
using System;
using Microsoft.PythonTools.Django.TemplateParsing;

namespace Microsoft.PythonTools.Django.Intellisense {
    class DjangoCompletionSource : ICompletionSource {
        private readonly DjangoCompletionSourceProvider _provider;
        private readonly ITextBuffer _buffer;

        public DjangoCompletionSource(DjangoCompletionSourceProvider djangoCompletionSourceProvider, ITextBuffer textBuffer) {
            _provider = djangoCompletionSourceProvider;
            _buffer = textBuffer;
        }

        #region ICompletionSource Members

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            DjangoProject project;
            string filename = _buffer.GetFilePath();
            if (filename != null) {
                project = DjangoPackage.GetProject(filename);
                TemplateTokenKind kind;
                if (project != null &&
                    _buffer.Properties.TryGetProperty<TemplateTokenKind>(typeof(TemplateTokenKind), out kind)) {

                        if (kind == TemplateTokenKind.Block || kind == TemplateTokenKind.Variable) {
                            var compSet = new CompletionSet();

                            List<Completion> completions = GetCompletions(project, kind);
                            completionSets.Add(new CompletionSet(
                                "Django Tags",
                                "Django Tags",
                                session.CreateTrackingSpan(_buffer),
                                completions.ToArray(),
                                new Completion[0])
                            );
                        }
                }
            }
        }

        private List<Completion> GetCompletions(DjangoProject project, TemplateTokenKind kind) {
            List<Completion> completions = new List<Completion>();
            var tags = kind == TemplateTokenKind.Block ? project._tags : project._filters;

            foreach (var tag in tags.OrderBy(x => x.Key, StringComparer.Ordinal)) {
                completions.Add(
                    new Completion(
                        tag.Key,
                        tag.Key,
                        "",
                        _provider._glyphService.GetGlyph(
                            StandardGlyphGroup.GlyphKeyword,
                            StandardGlyphItem.GlyphItemPublic
                        ),
                        "tag"
                    )
                );
            }
            return completions;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }
}

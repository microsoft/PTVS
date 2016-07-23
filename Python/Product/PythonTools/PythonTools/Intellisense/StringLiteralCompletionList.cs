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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides file path completion
    /// </summary>
    internal class StringLiteralCompletionList : CompletionAnalysis {
        internal StringLiteralCompletionList(IServiceProvider serviceProvider, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(serviceProvider, session, view, span, textBuffer, options) {
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var snapshot = TextBuffer.CurrentSnapshot;
            var span = snapshot.GetApplicableSpan(Span.GetStartPoint(snapshot));
            var text = span.GetText(snapshot);

            int start = text.IndexOfAny("'\"".ToCharArray());
            if (start < 0) {
                return null;
            }
            var prefix = text.Substring(0, start);
            var quote = text[start];
            var dir = text.Substring(start + 1);
            if (!Directory.Exists(dir)) {
                dir = PathUtils.GetParent(dir);
                if (!Directory.Exists(dir)) {
                    return null;
                }
            }

            var dirs = PathUtils.EnumerateDirectories(dir, recurse: false, fullPaths: true)
                .Select(d => new DynamicallyVisibleCompletion(
                    PathUtils.GetFileOrDirectoryName(d),
                    "r\"" + d + "\\\"",
                    d,
                    glyphService.GetGlyph(StandardGlyphGroup.GlyphClosedFolder, StandardGlyphItem.GlyphItemPublic),
                    "Folder"
                ));
            var files = PathUtils.EnumerateFiles(dir, recurse: false, fullPaths: true)
                .Select(f => new DynamicallyVisibleCompletion(
                    PathUtils.GetFileOrDirectoryName(f),
                    "r\"" + f + "\"",
                    f,
                    glyphService.GetGlyph(StandardGlyphGroup.GlyphLibrary, StandardGlyphItem.GlyphItemPublic),
                    "File"
                ));

            Session.Committed += Session_Committed;

            return new FuzzyCompletionSet(
                "PythonFilenames",
                "Files",
                Span,
                dirs.Concat(files),
                _options,
                CompletionComparer.UnderscoresLast,
                matchInsertionText: true
            );
        }

        private void Session_Committed(object sender, EventArgs e) {
            View.Caret.MoveTo(View.Caret.Position.BufferPosition.Subtract(1));
        }
    }
}

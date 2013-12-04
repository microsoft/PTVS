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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal abstract class DjangoCompletionSourceBase : ICompletionSource {
        protected readonly IGlyphService _glyphService;
        protected readonly DjangoAnalyzer _analyzer;
        protected readonly ITextBuffer _buffer;

        internal static readonly Dictionary<string, string> _nestedTags = new Dictionary<string, string>() {
            { "for", "endfor" },
            { "if", "endif" },
            { "ifequal", "endifequal" },
            { "ifnotequal", "endifnotequal" },
            { "ifchanged", "endifchanged" },
            { "autoescape", "endautoescape" },
            { "comment", "endcomment" },
            { "filter", "endfilter" },
            { "spaceless", "endspaceless" },
            { "with", "endwith" },
            { "empty", "endfor" },
            { "else", "endif" },
        };

        internal static readonly HashSet<string> _nestedEndTags = MakeNestedEndTags();
        private static readonly HashSet<string> _nestedStartTags = MakeNestedStartTags();

        protected DjangoCompletionSourceBase(IGlyphService glyphService, DjangoAnalyzer analyzer, ITextBuffer textBuffer) {
            _glyphService = glyphService;
            _analyzer = analyzer;
            _buffer = textBuffer;
        }

        #region ICompletionSource Members

        public abstract void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets);

        /// <param name="kind">The type of template tag we are processing</param>
        /// <param name="templateText">The text of the template tag which we are offering a completion in</param>
        /// <param name="templateStart">The offset in the buffer where the template starts</param>
        /// <param name="triggerPoint">The point in the buffer where the completion was triggered</param>
        internal CompletionSet GetCompletionSet(CompletionOptions options, DjangoAnalyzer analyzer, TemplateTokenKind kind, string templateText, int templateStart, SnapshotPoint triggerPoint, out ITrackingSpan applicableSpan) {
            int position = triggerPoint.Position - templateStart;
            IEnumerable<CompletionInfo> tags;
            IDjangoCompletionContext context;

            applicableSpan = GetWordSpan(templateText, templateStart, triggerPoint);

            switch (kind) {
                case TemplateTokenKind.Block:
                    var block = DjangoBlock.Parse(templateText);
                    if (block != null) {
                        if (position <= block.ParseInfo.Start + block.ParseInfo.Command.Length) {
                            // we are completing before the command
                            // TODO: Return a new set of tags?  Do nothing?  Do this based upon ctrl-space?
                            tags = FilterBlocks(CompletionInfo.ToCompletionInfo(analyzer._tags, StandardGlyphGroup.GlyphKeyword), triggerPoint);
                        } else {
                            // we are in the arguments, let the block handle the completions
                            context = new ProjectBlockCompletionContext(analyzer, _buffer);
                            tags = block.GetCompletions(context, position);
                        }
                    } else {
                        // no tag entered yet, provide the known list of tags.
                        tags = FilterBlocks(CompletionInfo.ToCompletionInfo(analyzer._tags, StandardGlyphGroup.GlyphKeyword), triggerPoint);
                    }
                    break;
                case TemplateTokenKind.Variable:
                    var variable = DjangoVariable.Parse(templateText);
                    context = new ProjectBlockCompletionContext(analyzer, _buffer);
                    if (variable != null) {
                        tags = variable.GetCompletions(context, position);
                    } else {
                        // show variable names
                        tags = CompletionInfo.ToCompletionInfo(context.Variables, StandardGlyphGroup.GlyphGroupVariable);
                    }

                    break;
                default:
                    throw new InvalidOperationException();
            }

            var completions = tags
                .OrderBy(tag => tag.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(tag => new DynamicallyVisibleCompletion(
                    tag.DisplayText,
                    tag.InsertionText,
                    StripDocumentation(tag.Documentation),
                    _glyphService.GetGlyph(tag.Glyph, StandardGlyphItem.GlyphItemPublic),
                    "tag"));
            return new FuzzyCompletionSet(
                "PythonDjangoTags",
                "Django Tags",
                applicableSpan,
                completions,
                options,
                CompletionComparer.UnderscoresLast);
        }

        private ITrackingSpan GetWordSpan(string templateText, int templateStart, SnapshotPoint triggerPoint) {
            ITrackingSpan applicableSpan;
            int spanStart = triggerPoint.Position;
            for (int i = triggerPoint.Position - templateStart - 1; i >= 0 && i < templateText.Length; --i, --spanStart) {
                char c = templateText[i];
                if (!char.IsLetterOrDigit(c) && c != '_') {
                    break;
                }
            }
            int length = triggerPoint.Position - spanStart;
            for (int i = triggerPoint.Position; i < triggerPoint.Snapshot.Length; i++) {
                char c = triggerPoint.Snapshot[i];
                if (!char.IsLetterOrDigit(c) && c != '_') {
                    break;
                }
                length++;
            }

            applicableSpan = triggerPoint.Snapshot.CreateTrackingSpan(
                spanStart,
                length,
                SpanTrackingMode.EdgeInclusive
            );

            return applicableSpan;
        }

        internal static string StripDocumentation(string doc) {
            if (doc == null) {
                return String.Empty;
            }
            StringBuilder result = new StringBuilder(doc.Length);
            foreach (string line in doc.Split('\n')) {
                if (result.Length > 0) {
                    result.Append("\r\n");
                }
                result.Append(line.Trim());
            }
            return result.ToString();
        }

        protected abstract IEnumerable<DjangoBlock> GetBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint);

        private IEnumerable<CompletionInfo> FilterBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint) {
            int depth = 0;
            HashSet<string> included = new HashSet<string>();
            foreach (var block in GetBlocks(results, triggerPoint)) {
                var cmd = block.ParseInfo.Command;
                if (cmd == "elif") {
                    if (depth == 0) {
                        included.Add("endif");
                    }
                    // otherwise elif both starts and ends a block, 
                    // so depth remains the same.
                } else if (_nestedEndTags.Contains(cmd)) {
                    depth++;
                } else if (_nestedStartTags.Contains(cmd)) {
                    if (depth == 0) {
                        included.Add(_nestedTags[cmd]);
                        if (cmd == "if") {
                            included.Add("elif");
                        }
                    }

                    // we happily let depth go negative, it'll prevent us from
                    // including an end tag for outer blocks when we're in an 
                    // inner block.
                    depth--;
                }
            }

            foreach (var value in results) {
                if (!(_nestedEndTags.Contains(value.DisplayText) || value.DisplayText == "elif") ||
                    included.Contains(value.DisplayText)) {
                    yield return value;
                }
            }
        }

        #endregion

        private static HashSet<string> MakeNestedEndTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var value in _nestedTags.Values) {
                res.Add(value);
            }
            return res;
        }

        private static HashSet<string> MakeNestedStartTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var key in _nestedTags.Keys) {
                res.Add(key);
            }
            return res;
        }


        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }
}

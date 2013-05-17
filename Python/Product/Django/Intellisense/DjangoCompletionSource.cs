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
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    class DjangoCompletionSource : ICompletionSource {
        private readonly DjangoCompletionSourceProvider _provider;
        private readonly ITextBuffer _buffer;

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

        public DjangoCompletionSource(DjangoCompletionSourceProvider djangoCompletionSourceProvider, ITextBuffer textBuffer) {
            _provider = djangoCompletionSourceProvider;
            _buffer = textBuffer;
        }

        #region ICompletionSource Members

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            string filename = _buffer.GetFilePath();
            if (filename == null) {
                return;
            }
            var project = DjangoPackage.GetProject(filename);
            if (project == null) {
                return;
            }
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
                
                var completions = GetCompletions(
                    project,
                    kind,
                    templateText,
                    templateStart,
                    triggerPoint,
                    out applicableSpan);

                completionSets.Add(
                    new FuzzyCompletionSet(
                        "PythonDjangoTags",
                        "Django Tags",
                        applicableSpan,
                        completions,
                        session.GetOptions(),
                        CompletionComparer.UnderscoresLast
                    )
                );
            }
        }

        class ProjectBlockCompletionContext : IDjangoCompletionContext {
            private readonly DjangoProject _project;
            private readonly string _filename;
            private readonly HashSet<string> _loopVars;

            public ProjectBlockCompletionContext(DjangoProject project, ITextBuffer buffer) {
                _project = project;
                _filename = buffer.GetFilePath();
                TemplateProjectionBuffer projBuffer;
                if (buffer.Properties.TryGetProperty(typeof(TemplateProjectionBuffer), out projBuffer)) {
                    foreach (var span in projBuffer.Spans) {
                        if (span.Block != null) {
                            foreach (var variable in span.Block.GetVariables()) {
                                if (_loopVars == null) {
                                    _loopVars = new HashSet<string>();
                                }
                                _loopVars.Add(variable);
                            }
                        }
                    }
                }
            }

            public Dictionary<string, HashSet<AnalysisValue>> Variables {
                get {
                    var res = GetVariablesForTemplateFile(_project, _filename);
                    if (_loopVars != null) {
                        if (res == null) {
                            res = new Dictionary<string, HashSet<AnalysisValue>>();
                        } else {
                            res = new Dictionary<string, HashSet<AnalysisValue>>(res);
                        }

                        foreach (var loopVar in _loopVars) {
                            if (!res.ContainsKey(loopVar)) {
                                res[loopVar] = new HashSet<AnalysisValue>();
                            }
                        }
                    }
                    return res;
                }
            }

            public Dictionary<string, TagInfo> Filters {
                get {
                    return _project._filters;
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="project"></param>
        /// <param name="kind">The type of template tag we are processing</param>
        /// <param name="templateText">The text of the template tag which we are offering a completion in</param>
        /// <param name="templateStart">The offset in the buffer where the template starts</param>
        /// <param name="triggerPoint">The point in the buffer where the completion was triggered</param>
        /// <returns></returns>
        private IEnumerable<DynamicallyVisibleCompletion> GetCompletions(DjangoProject project, TemplateTokenKind kind, string templateText, int templateStart, SnapshotPoint triggerPoint, out ITrackingSpan applicableSpan) {
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
                            tags = FilterBlocks(CompletionInfo.ToCompletionInfo(project._tags, StandardGlyphGroup.GlyphKeyword), triggerPoint);
                        } else {
                            // we are in the arguments, let the block handle the completions
                            context = new ProjectBlockCompletionContext(project, _buffer);
                            tags = block.GetCompletions(context, position);
                        }
                    } else {
                        // no tag entered yet, provide the known list of tags.
                        tags = FilterBlocks(CompletionInfo.ToCompletionInfo(project._tags, StandardGlyphGroup.GlyphKeyword), triggerPoint);
                    }
                    break;
                case TemplateTokenKind.Variable:
                    var variable = DjangoVariable.Parse(templateText);
                    context = new ProjectBlockCompletionContext(project, _buffer);
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

            return tags
                .OrderBy(tag => tag.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(tag => new DynamicallyVisibleCompletion(
                                    tag.DisplayText,
                                    tag.InsertionText,
                                    StripDocumentation(tag.Documentation),
                                    _provider._glyphService.GetGlyph(tag.Glyph, StandardGlyphItem.GlyphItemPublic),
                                    "tag"));
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

            applicableSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(
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

        private IEnumerable<CompletionInfo> FilterBlocks(IEnumerable<CompletionInfo> results, SnapshotPoint triggerPoint) {
            var projBuffer = _buffer.Properties.GetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer));
            var regions = projBuffer.GetTemplateRegions(
                new SnapshotSpan(new SnapshotPoint(triggerPoint.Snapshot, 0), triggerPoint),
                reversed: true
            );

            int depth = 0;
            HashSet<string> included = new HashSet<string>();
            foreach (var region in regions) {
                if (region.Kind == TemplateTokenKind.Block && region.Block != null) {
                    var cmd = region.Block.ParseInfo.Command;

                    if (_nestedEndTags.Contains(cmd)) {
                        depth++;
                    } else if (_nestedStartTags.Contains(cmd)) {
                        if (depth == 0) {
                            included.Add(_nestedTags[cmd]);
                        }

                        // we happily let depth go negative, it'll prevent us from
                        // including an end tag for outer blocks when we're in an 
                        // inner block.
                        depth--;
                    }
                }
            }

            foreach (var value in results) {
                if (!_nestedEndTags.Contains(value.DisplayText) ||
                    included.Contains(value.DisplayText)) {
                    yield return value;
                }
            }
        }

        private static Dictionary<string, HashSet<AnalysisValue>> GetVariablesForTemplateFile(DjangoProject project, string filename) {
            string curLevel = filename;                     // is C:\Foo\Bar\Baz\foo.html
            string curPath = filename = Path.GetFileName(filename);    // is foo.html

            for (; ; ) {
                string curFilename = filename.Replace('\\', '/');
                Dictionary<string, HashSet<AnalysisValue>> res;
                if (project._templateFiles.TryGetValue(curFilename, out res)) {
                    return res;
                }
                curLevel = Path.GetDirectoryName(curLevel);  // C:\Foo\Bar\Baz\foo.html gets us C:\Foo\Bar\Baz
                var fn2 = Path.GetFileName(curLevel);            // Gets us Baz
                if (String.IsNullOrEmpty(fn2)) {
                    break;
                }
                curPath = Path.Combine(fn2, curPath);       // Get us Baz\foo.html       
                filename = curPath;
            }

            return null;
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

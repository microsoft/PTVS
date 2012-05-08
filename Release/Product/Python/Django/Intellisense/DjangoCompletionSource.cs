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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

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
                TemplateProjectionBuffer projBuffer;
                TemplateTokenKind kind;
                string templateText;
                var triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
                int templateStart;
                if (project != null &&
                    triggerPoint != null &&
                    _buffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer) &&
                    (templateText = projBuffer.GetTemplateText(triggerPoint.Value, out kind, out templateStart)) != null) {

                    if (kind == TemplateTokenKind.Block || kind == TemplateTokenKind.Variable) {
                        var compSet = new CompletionSet();

                        List<Completion> completions = GetCompletions(
                            project,
                            kind,
                            templateText,
                            templateStart,
                            session.GetTriggerPoint(_buffer.CurrentSnapshot));

                        completionSets.Add(
                            new CompletionSet(
                                "Django Tags",
                                "Django Tags",
                                session.CreateTrackingSpan(_buffer),
                                completions.ToArray(),
                                new Completion[0]
                            )
                        );
                    }
                }
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

            public Dictionary<string, HashSet<AnalysisValue>> Filters {
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
        /// <param name="bufferText">The text of the template tag which we are offering a completion in</param>
        /// <param name="templateStart">the offset in the buffer where teh template starts</param>
        /// <param name="triggerPoint">The point in the buffer where the completion was triggered</param>
        /// <returns></returns>
        private List<Completion> GetCompletions(DjangoProject project, TemplateTokenKind kind, string bufferText, int templateStart, SnapshotPoint? triggerPoint) {
            List<Completion> completions = new List<Completion>();
            IEnumerable<CompletionInfo> tags;

            switch (kind) {
                case TemplateTokenKind.Block:
                    var block = DjangoBlock.Parse(bufferText);
                    if (block != null && triggerPoint != null) {
                        int position = triggerPoint.Value.Position - templateStart;
                        if (position <= block.ParseInfo.Start) {
                            // we are completing before the command
                            // TODO: Return a new set of tags?  Do nothing?  Do this based upon ctrl-space?
                            tags = CompletionInfo.ToCompletionInfo(project._tags.Keys, StandardGlyphGroup.GlyphKeyword);
                        } else if (position <= block.ParseInfo.Start + block.ParseInfo.Command.Length) {
                            // we are completing in the middle of the command, we should filter based upon
                            // the command text up to the current position
                            tags = DjangoVariable.FilterTags(
                                project._tags.Keys,
                                block.ParseInfo.Command.Substring(0, position - block.ParseInfo.Start)
                            );
                        } else {
                            // we are in the arguments, let the block handle the completions
                            tags = block.GetCompletions(
                                new ProjectBlockCompletionContext(project, _buffer),
                                position
                            );
                        }
                    } else {
                        // no tag entered yet, provide the known list of tags.
                        tags = CompletionInfo.ToCompletionInfo(project._tags.Keys, StandardGlyphGroup.GlyphKeyword);
                    }
                    break;
                case TemplateTokenKind.Variable:
                    tags = CompletionInfo.ToCompletionInfo(project._filters.Keys, StandardGlyphGroup.GlyphKeyword);
                    var filePath = _buffer.GetFilePath();

                    var dirName = Path.GetDirectoryName(filePath);

                    var variable = DjangoVariable.Parse(bufferText);
                    if (variable != null && triggerPoint != null) {
                        int position = triggerPoint.Value.Position - templateStart;
                        tags = variable.GetCompletions(
                            new ProjectBlockCompletionContext(project, _buffer),
                            position
                        );
                    } else {
                        // show variable names
                        var tempTags = GetVariablesForTemplateFile(project, filePath);
                        if (tempTags != null) {
                            tags = CompletionInfo.ToCompletionInfo(tempTags.Keys, StandardGlyphGroup.GlyphKeyword);
                        } else {
                            tags = new CompletionInfo[0];
                        }
                    }

                    break;
                default:
                    throw new InvalidOperationException();
            }

            foreach (var tag in tags.OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase)) {
                completions.Add(
                    new Completion(
                        tag.DisplayText,
                        tag.InsertionText,
                        "",
                        _provider._glyphService.GetGlyph(
                            tag.Glyph,
                            StandardGlyphItem.GlyphItemPublic
                        ),
                        "tag"
                    )
                );
            }
            return completions;
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

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }
}

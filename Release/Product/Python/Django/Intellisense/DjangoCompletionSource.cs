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
                if (project != null &&
                    triggerPoint != null &&
                    _buffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer) &&
                    (templateText = projBuffer.GetTemplateText(triggerPoint.Value, out kind)) != null) {

                    if (kind == TemplateTokenKind.Block || kind == TemplateTokenKind.Variable) {
                        var compSet = new CompletionSet();

                        List<Completion> completions = GetCompletions(
                            project, 
                            kind, 
                            templateText, 
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

        private List<Completion> GetCompletions(DjangoProject project, TemplateTokenKind kind, string bufferText, SnapshotPoint? triggerPoint) {
            List<Completion> completions = new List<Completion>();
            IEnumerable<string> tags;

            var glyph = StandardGlyphGroup.GlyphKeyword;

            switch (kind) {
                case TemplateTokenKind.Block:
                    tags = project._tags.Keys;
                    break;
                case TemplateTokenKind.Variable:
                    tags = project._filters.Keys;
                    var filePath = this._buffer.GetFilePath();

                    var dirName = Path.GetDirectoryName(filePath);

                    var variable = DjangoVariable.Parse(bufferText);
                    if (variable != null && triggerPoint != null && variable.Expression != null) {
                        int position = triggerPoint.Value.Position;

                        if (position == variable.Expression.Value.Length + variable.ExpressionStart) {
                            var tempTags = GetVariablesForTemplateFile(project, filePath);
                            // TODO: Handle multiple dots
                            if (variable.Expression.Value.EndsWith(".")) {
                                // get the members of this variable
                                if (tempTags != null) {
                                    HashSet<string> newTags = new HashSet<string>();
                                    foreach (var value in tempTags.Values) {
                                        foreach (var item in value) {
                                            foreach (var members in item.GetAllMembers()) {
                                                newTags.Add(members.Key);
                                            }
                                        }
                                    }
                                    tags = newTags;
                                }
                            } else {
                                tags = FilterTags(tempTags.Keys, variable.Expression.Value);
                            }
                        } else if (position < variable.Expression.Value.Length + variable.ExpressionStart) {
                            // we are triggering in the variable name area, we need to return variables
                            // but we need to filter them.
                            glyph = StandardGlyphGroup.GlyphGroupField;
                            var tempTags = GetVariablesForTemplateFile(project, filePath);
                            if (tempTags != null) {
                                tags = tempTags.Keys;
                            } else {
                                tags = new string[0];
                            }
                        } else {
                            // we are triggering in the filter or arg area
                            for (int i = 0; i < variable.Filters.Length; i++) {
                                var curFilter = variable.Filters[i];
                                if (position >= curFilter.FilterStart &&
                                    position < curFilter.FilterStart + curFilter.Filter.Length) {
                                    // it's in this filter area
                                    tags = FilterFilters(project, variable.Expression.Value);
                                    break;
                                } else if (curFilter.Arg != null) {
                                    if (position >= curFilter.ArgStart &&
                                        position < curFilter.ArgStart + curFilter.Arg.Value.Length) {
                                        // it's in this argument
                                        glyph = StandardGlyphGroup.GlyphGroupField;
                                        var tempTags = GetVariablesForTemplateFile(project, filePath);
                                        if (tempTags != null) {
                                            tags = tempTags.Keys;
                                        } else {
                                            tags = new string[0];
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    } else {
                        // show variable names
                        var tempTags = GetVariablesForTemplateFile(project, filePath);
                        if (tempTags != null) {
                            tags = tempTags.Keys;
                        } else {
                            tags = new string[0];
                        }

                        glyph = StandardGlyphGroup.GlyphGroupField;
                    }

                    break;
                default:
                    throw new InvalidOperationException();
            }

            foreach (var tag in tags.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                completions.Add(
                    new Completion(
                        tag,
                        tag,
                        "",
                        _provider._glyphService.GetGlyph(
                            glyph,
                            StandardGlyphItem.GlyphItemPublic
                        ),
                        "tag"
                    )
                );
            }
            return completions;
        }

        private Dictionary<string, HashSet<AnalysisValue>> GetVariablesForTemplateFile(DjangoProject project, string filename) {
            string curLevel = filename;                     // is C:\Foo\Bar\Baz\foo.html
            string curPath = Path.GetFileName(filename);    // is foo.html

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

        private static IEnumerable<string> FilterFilters(DjangoProject project, string filter) {
            return from tag in project._filters.Keys where tag.StartsWith(filter) select tag;
        }

        private static IEnumerable<string> FilterTags(IEnumerable<string> keys, string filter) {
            return from tag in keys where tag.StartsWith(filter) select tag;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }

}

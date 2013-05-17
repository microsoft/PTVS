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


using System.Collections.Generic;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class HtmlCompletionSource : ICompletionSource {
        private readonly HtmlCompletionSourceProvider _completionSourceProvider;
        private readonly ITextBuffer _textBuffer;


        private static string[] _htmlTags = new[] {  "!doctype",  "a",  "abbr",  "acronym",  "address",  "applet",  "area",  "b",  "base",  "basefont",  "bdo",  "big",  "blockquote",  
                                                      "body",  "br",  "button",  "caption",  "center",  "cite", "code",  "col",  "colgroup",  "dd",  "del",  "dfn",  "dir",  "div",  
                                                      "dl",  "dt",  "em",  "fieldset",  "font",  "form",  "frame",  "frameset",  "h1",  "h2",  "h3",  "h4",  "h5",  "h6",  "head", 
                                                      "hr",  "html",  "i",  "iframe",  "img",  "input",  "ins",  "isindex",  "kbd",  "label",  "legend",  "li",  "link",  "map",  "menu",  
                                                      "meta",  "noframes",  "noscript",  "object",  "ol",  "optgroup", "option",  "p",  "param",  "pre",  "q",  "s",  "samp",  "script",  
                                                      "select",  "small",  "span",  "strike",  "strong",  "style",  "sub",  "sup",  "table",  "tbody",  "td",  "textarea",  "tfoot", 
                                                      "th",  "thead",  "title",  "tr",  "tt",  "u",  "ul",  "var"};

        public HtmlCompletionSource(HtmlCompletionSourceProvider completionSourceProvider, ITextBuffer textBuffer) {
            _completionSourceProvider = completionSourceProvider;
            _textBuffer = textBuffer;
        }

        #region ICompletionSource Members

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            TemplateProjectionBuffer projBuffer;
            if (_textBuffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer)) {
                var completions = new List<DynamicallyVisibleCompletion>();
                foreach (var tag in _htmlTags) {
                    completions.Add(new DynamicallyVisibleCompletion(
                        tag,
                        tag,
                        "",
                        _completionSourceProvider._glyphService.GetGlyph(StandardGlyphGroup.GlyphXmlItem, StandardGlyphItem.GlyphItemPublic),
                        "")
                    );
                }
                foreach (var tag in _htmlTags) {
                    completions.Add(new DynamicallyVisibleCompletion(
                        "/" + tag,
                        "/" + tag,
                        "",
                        _completionSourceProvider._glyphService.GetGlyph(StandardGlyphGroup.GlyphXmlItem, StandardGlyphItem.GlyphItemPublic),
                        "")
                    );
                }

                //
                var triggerPoint = session.GetTriggerPoint(_textBuffer);
                var point = triggerPoint.GetPoint(_textBuffer.CurrentSnapshot);

                var match = projBuffer.BufferGraph.MapUpToFirstMatch(
                    point,
                    PointTrackingMode.Positive,
                    x => x.TextBuffer != _textBuffer,
                    PositionAffinity.Predecessor
                );
                
                if (match == null ||
                    !match.Value.Snapshot.TextBuffer.ContentType.IsOfType(TemplateContentType.ContentTypeName)) {

                    var line = point.GetContainingLine();
                    var text = line.GetText();
                    int position = point.Position;
                    for (int i = position - line.Start.Position - 1; i >= 0 && i < text.Length; --i, --position) {
                        char c = text[i];
                        if (!char.IsLetterOrDigit(c) && c != '!') {
                            break;
                        }
                    }

                    var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(position, point.Position - position, SpanTrackingMode.EdgeInclusive);

                    completionSets.Add(new FuzzyCompletionSet("PythonDjangoTemplateHtml", "HTML", span, completions, session.GetOptions(), CompletionComparer.UnderscoresLast));
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }
}

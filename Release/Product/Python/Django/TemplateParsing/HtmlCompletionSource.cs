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
            List<Completion> completions = new List<Completion>();
            foreach (var tag in _htmlTags) {
                completions.Add(new Completion(
                    tag, 
                    "<" + tag, 
                    "", 
                    _completionSourceProvider._glyphService.GetGlyph(StandardGlyphGroup.GlyphXmlAttribute, StandardGlyphItem.GlyphItemPublic), 
                    "")
                );
            }

            var triggerPoint = session.GetTriggerPoint(_textBuffer);

            var position = triggerPoint.GetPosition(_textBuffer.CurrentSnapshot);

            var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(position, 0, SpanTrackingMode.EdgeInclusive);
            
            completionSets.Add(new HtmlCompletionSet("", "", span, completions, new Completion[0]));
        }

        class HtmlCompletionSet : CompletionSet {
            public HtmlCompletionSet(string moniker, string displayName, ITrackingSpan applicableTo, IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders) :
                base(moniker, displayName, applicableTo, completions, completionBuilders) {
            }

            public override void SelectBestMatch() {
                SelectBestMatch(CompletionMatchType.MatchInsertionText, true);
                if (!SelectionStatus.IsSelected) {
                    SelectBestMatch(CompletionMatchType.MatchInsertionText, false);
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

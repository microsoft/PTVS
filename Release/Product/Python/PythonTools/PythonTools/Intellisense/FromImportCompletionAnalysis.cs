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
using System.Diagnostics;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is doing an "import from"
    /// </summary>
    internal class FromImportCompletionAnalysis : CompletionAnalysis {
        private readonly string _namespace;

        public FromImportCompletionAnalysis(string text, int pos, ITrackingSpan span, ITextBuffer textBuffer, string ns)
            : base(text, pos, span, textBuffer) {
            _namespace = ns;
        }

        public static CompletionAnalysis Make(IList<ClassificationSpan> classifications, ClassificationSpan start,
                Span loc, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer buffer, bool isSpace) {
            if (classifications.Count == 1) {
                return new ImportCompletionAnalysis(String.Empty, loc.Start, span, buffer);
            }

            ClassificationSpan imp = null;
            for (int i = 1; i < classifications.Count; i++) {
                if (IsKeyword(classifications[i], "import")) {
                    imp = classifications[i];
                }
            }

            var end = classifications[classifications.Count - 1];
            if (imp == null) {
                if (isSpace) {
                    return EmptyCompletionContext;
                }

                // from [completion]
                //  or
                // from xxx.[completion]
                //  or
                // from xxx[Ctrl-Space completion]
                return new ImportCompletionAnalysis(GetText(snapshot, start, end, true), loc.Start, span, buffer);
            }

            // from xyz import [completion]
            //  or
            // from xyz import abc[Ctrl-Space completion]
            var nsText = GetText(snapshot, start, imp, false);

            string itemText;
            if (Object.ReferenceEquals(end, imp)) {
                itemText = String.Empty;
            } else {
                var itemLen = end.Span.End - imp.Span.End - 1;
                var itemSpan = new SnapshotSpan(snapshot, imp.Span.End + 1, itemLen);
                itemText = itemSpan.GetText();
                string trimmedText = itemText.TrimEnd();
                int spaceIndex;
                if ((spaceIndex = trimmedText.LastIndexOfAny(new[] { ' ', '\t' })) != -1) {
                    itemText = itemText.Substring(spaceIndex + 1);
                }

                if (isSpace) {
                    if (!trimmedText.EndsWith(",")) {
                        return EmptyCompletionContext;
                    } else {
                        itemText = "";
                    }
                }
            }

            return new FromImportCompletionAnalysis(itemText, loc.Start, span, buffer, nsText);
        }

        private static string GetText(ITextSnapshot snapshot, ClassificationSpan start, ClassificationSpan target, bool includeEnd) {
            var nsLen = (includeEnd ? target.Span.End : target.Span.Start) - start.Span.End - 1;
            var nsSpan = new SnapshotSpan(snapshot, start.Span.End + 1, nsLen);
            var nsText = nsSpan.GetText().Trim();
            return nsText;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            string text = _namespace + "." + Text;
            var completions = FromCompletions(glyphService, GetModules(glyphService, text, includeMembers: true));
            var res = new PythonCompletionSet(Text, Text, Span, completions, new Completion[0]);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} imports", this, end - start, res.Completions.Count));
            }

            return res;
        }
        
        private IEnumerable<Completion> FromCompletions(IGlyphService glyphService, IEnumerable<Completion> inputs) {
            foreach (var input in inputs) {
                if (input.InsertionText.IndexOf('.') == -1) {
                    yield return input;
                }
            }

            if (String.IsNullOrWhiteSpace(Text)) {
                yield return PythonCompletion(glyphService, "*", "Import all members from the module", StandardGlyphGroup.GlyphArrow);
            }
        }
    }

    class PythonCompletionSet : CompletionSet {
        public PythonCompletionSet(string moniker, string displayName, ITrackingSpan applicableTo, IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders) :
            base(moniker, displayName, applicableTo, completions, completionBuilders) {
        }

        public override void SelectBestMatch() {
            SelectBestMatch(CompletionMatchType.MatchInsertionText, true);
            if (!SelectionStatus.IsSelected) {
                SelectBestMatch(CompletionMatchType.MatchInsertionText, false);
            }
        }
    }
}

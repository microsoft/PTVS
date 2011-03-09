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
using System.Diagnostics;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides various completion services after the text around the current location has been
    /// processed. The completion services are specific to the current context
    /// </summary>
    public class CompletionAnalysis {
        private readonly string _text;
        protected readonly int _pos;
        private readonly ITrackingSpan _span;
        private readonly ITextBuffer _textBuffer;
        internal const Int64 TooMuchTime = 50;
        protected static Stopwatch _stopwatch = MakeStopWatch();

        internal static CompletionAnalysis EmptyCompletionContext = new CompletionAnalysis(String.Empty, 0, null, null);

        internal CompletionAnalysis(string text, int pos, ITrackingSpan span, ITextBuffer textBuffer) {
            _text = text ?? String.Empty;
            _pos = pos;
            _span = span;
            _textBuffer = textBuffer;
        }

#if FALSE
        /// <summary>
        /// Gets a CompletionContext providing a list of possible members the user can dot through.
        /// </summary>
        public static CompletionContext GetMemberCompletionContext(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span) {
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);
            var line = parser.Snapshot.GetLineFromPosition(loc.Start);
            var lineStart = line.Start;

            var textLen = loc.End - lineStart.Position;
            if (textLen <= 0) {
                // Ctrl-Space on an empty line, we just want to get global vars
                return new NormalCompletionContext(String.Empty, loc.Start, parser.Snapshot, parser.Span, parser.Buffer, 0);
            }

            return TrySpecialCompletions(parser, loc) ??
                   GetNormalCompletionContext(parser, loc);
        }

        /// <summary>
        /// Gets a CompletionContext for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        public static CompletionContext GetExpressionContext(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span) {
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);
            var exprRange = parser.GetExpressionRange();
            if (exprRange == null) {
                return EmptyCompletionContext;
            }

            // extend right for any partial expression the user is hovering on, for example:
            // "x.Baz" where the user is hovering over the B in baz we want the complete
            // expression.
            var text = exprRange.Value.GetText();
            var endingLine = exprRange.Value.End.GetContainingLine();
            var endText = snapshot.GetText(exprRange.Value.End.Position, endingLine.End.Position - exprRange.Value.End.Position);
            for (int i = 0; i < endText.Length; i++) {
                if (!Char.IsLetterOrDigit(endText[i]) && endText[i] != '_') {
                    text += endText.Substring(0, i);
                    break;
                }
            }

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            return new NormalCompletionContext(
                text,
                loc.Start,
                parser.Snapshot,
                applicableSpan,
                parser.Buffer,
                -1
            );
        }

        public static CompletionContext GetSignatureContext(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span) {
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, buffer, span);

            var loc = parser.Span.GetSpan(parser.Snapshot.Version);

            int paramIndex;
            var exprRange = parser.GetExpressionRange(1, out paramIndex);
            if (exprRange == null) {
                return EmptyCompletionContext;
            }
            var text = exprRange.Value.GetText();

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            return new NormalCompletionContext(
                text,
                loc.Start,
                parser.Snapshot,
                applicableSpan,
                parser.Buffer,
                paramIndex
            );
        }

#endif
        public ITextBuffer TextBuffer {
            get {
                return _textBuffer;
            }
        }

        public string Text {
            get {
                return _text;
            }
        }

        public ITrackingSpan Span {
            get {
                return _span;
            }
        }
#if FALSE
        public virtual int ParameterIndex {
            get { return 0; }
        }
#endif
        public virtual CompletionSet GetCompletions(IGlyphService glyphService) {
            return null;
        }

#if FALSE
        public virtual ISignature[] GetSignatures() {
            return new ISignature[0];
        }

        public virtual string GetQuickInfo() {
            return null;
        }

        public virtual IEnumerable<VariableResult> GetVariableInfo() {
            yield break;
        }

        private static CompletionContext GetNormalCompletionContext(ReverseExpressionParser parser, Span loc) {
            var exprRange = parser.GetExpressionRange();
            if (exprRange == null) {
                return EmptyCompletionContext;
            }
            var text = exprRange.Value.GetText();

            var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                exprRange.Value.Span,
                SpanTrackingMode.EdgeExclusive
            );

            return new NormalCompletionContext(
                text,
                loc.Start,
                parser.Snapshot,
                applicableSpan,
                parser.Buffer,
                -1
            );
        }

        private static CompletionContext TrySpecialCompletions(ReverseExpressionParser parser, Span loc) {
            if (parser.Tokens.Count > 0) {
                // Check for context-sensitive intellisense
                var lastClass = parser.Tokens[parser.Tokens.Count - 1];

                if (lastClass.ClassificationType == parser.Classifier.Provider.Comment) {
                    // No completions in comments
                    return EmptyCompletionContext;
                } else if (lastClass.ClassificationType == parser.Classifier.Provider.StringLiteral) {
                    // String completion
                    return new StringLiteralCompletionContext(lastClass.Span.GetText(), loc.Start, parser.Span, parser.Buffer);
                }

                // Import completions
                var first = parser.Tokens[0];
                if (IsKeyword(first, "import")) {
                    return ImportCompletionContext.Make(first, lastClass, loc, parser.Snapshot, parser.Span, parser.Buffer, IsSpaceCompletion(parser, loc));
                } else if (IsKeyword(first, "from")) {
                    return FromImportCompletionContext.Make(parser.Tokens, first, loc, parser.Snapshot, parser.Span, parser.Buffer, IsSpaceCompletion(parser, loc));
                }
                return null;
            }

            return EmptyCompletionContext;
        }

        private static bool IsSpaceCompletion(ReverseExpressionParser parser, Span loc) {
            var keySpan = new SnapshotSpan(parser.Snapshot, loc.Start - 1, 1);
            return (keySpan.GetText() == " ");
        }
#endif
        internal static bool IsKeyword(ClassificationSpan token, string keyword) {
            return token.ClassificationType.Classification == "keyword" && token.Span.GetText() == keyword;
        }

        internal static Completion PythonCompletion(IGlyphService service, MemberResult memberResult) {
            StandardGlyphGroup group = memberResult.MemberType.ToGlyphGroup();
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new LazyCompletion(memberResult.Name, () => memberResult.Completion, () => memberResult.Documentation, service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        internal static Completion PythonCompletion(IGlyphService service, string name, string tooltip, StandardGlyphGroup group) {
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new LazyCompletion(name, () => name, () => tooltip, service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        internal ModuleAnalysis GetAnalysisEntry() {
            return ((IPythonProjectEntry)TextBuffer.GetAnalysis()).Analysis;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        public virtual Completion[] GetModules(IGlyphService glyphService, string text) {
            var analysis = GetAnalysisEntry();
            var path = text.Split('.');
            if (path.Length > 0) {
                // path = path[:-1]
                var newPath = new string[path.Length - 1];
                Array.Copy(path, newPath, path.Length - 1);
                path = newPath;
            }

            MemberResult[] modules = new MemberResult[0];
            if (path.Length == 0) {
                if (analysis != null) {
                    modules = analysis.ProjectState.GetModules(true);
                }

#if REPL
                var repl = Intellisense.GetRepl(_textBuffer);
                if (repl != null) {
                    modules = Intellisense.MergeMembers(modules, repl.GetModules());
                }
#endif
            } else {
                if (analysis != null) {

                    modules = analysis.ProjectState.GetModuleMembers(analysis.InterpreterContext, path);
                }
            }

            var sortedAndFiltered = NormalCompletionAnalysis.FilterCompletions(modules, text, (x, y) => x.StartsWith(y));
            Array.Sort(sortedAndFiltered, NormalCompletionAnalysis.ModuleSort);

            var result = new Completion[sortedAndFiltered.Length];
            for (int i = 0; i < sortedAndFiltered.Length; i++) {
                result[i] = PythonCompletion(glyphService, sortedAndFiltered[i]);
            }
            return result;
        }

        public override string ToString() {
            return String.Format("CompletionContext({0}): {1} @{2}", GetType().Name, Text, _pos);
        }
    }
}

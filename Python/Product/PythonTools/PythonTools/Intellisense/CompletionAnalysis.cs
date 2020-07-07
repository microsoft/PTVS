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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.LanguageServer;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides various completion services after the text around the current location has been
    /// processed. The completion services are specific to the current context
    /// </summary>
    internal class CompletionAnalysis {
        internal const long TooMuchTime = 50;
        private static readonly Stopwatch _stopwatch = MakeStopWatch();

        private readonly PythonEditorServices _services;
        private readonly ICompletionSession _session;
        private readonly ITextView _view;
        private readonly ITextBuffer _textBuffer;
        private readonly ITextSnapshot _snapshot;
        private readonly ITrackingPoint _point;
        private readonly CompletionOptions _options;
        private ITrackingSpan _span;

        internal static CompletionAnalysis EmptyCompletionContext = new CompletionAnalysis(null, null, null, null, null, null);

        internal CompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITextSnapshot snapshot, ITrackingPoint point, CompletionOptions options) {
            _session = session;
            _view = view;
            _services = services;
            _snapshot = snapshot;
            _textBuffer = snapshot?.TextBuffer;
            _point = point;
            _options = options == null ? new CompletionOptions() : options.Clone();
        }

        public CompletionSet GetCompletions(IGlyphService glyphService) {
            if (_snapshot == null) {
                return null;
            }

            var start = _stopwatch.ElapsedMilliseconds;

            var interactiveWindow = _snapshot.TextBuffer.GetInteractiveWindow();
            var pyReplEval = interactiveWindow?.Evaluator as IPythonInteractiveIntellisense;

            var bufferInfo = PythonTextBufferInfo.TryGetForBuffer(_textBuffer);
            Debug.Assert(bufferInfo != null, "Getting completions from uninitialized buffer " + _textBuffer);

            var analysis = bufferInfo?.AnalysisEntry;
            var analyzer = analysis?.Analyzer;

            if (analyzer == null) {
                return null;
            }

            var point = _point.GetPoint(bufferInfo.CurrentSnapshot);

            var location = VsProjectAnalyzer.TranslateIndex(
                point.Position,
                point.Snapshot,
                analysis
            );

            var triggerChar = _session.GetTriggerCharacter();
            var completions = analyzer.WaitForRequest(analyzer.GetCompletionsAsync(
                analysis,
                location,
                _options.MemberOptions,
                triggerChar == '\0' ? CompletionTriggerKind.Invoked : CompletionTriggerKind.TriggerCharacter,
                triggerChar == '\0' ? null : triggerChar.ToString()
            ), "GetCompletions.GetMembers", null, pyReplEval?.Analyzer == null ? 1 : 5);

            if (completions == null) {
                return null;
            }

            var snapshotSpan = completions._applicableSpan.HasValue
                ? ((SourceSpan)completions._applicableSpan.Value).ToSnapshotSpan(_snapshot)
                : new SnapshotSpan(point, 0);
            _span = bufferInfo.CurrentSnapshot.CreateTrackingSpan(snapshotSpan, SpanTrackingMode.EdgeInclusive);
            var members = completions.items.MaybeEnumerate()
                .Where(c => c != null)
                .Select(c => new CompletionResult(
                // if a method stub generation (best guess by comparing length),
                // merge entry based on label, for everything else, use insertion text
                c.filterText ?? (c.insertText != null && c.insertText.Length <= c.label.Length ? c.insertText : c.label),
                c.label,
                c.insertText ?? c.label,
                c.documentation?.value,
                Enum.TryParse(c._kind, true, out PythonMemberType mt) ? mt : PythonMemberType.Unknown,
                null
            ));

            if (pyReplEval?.Analyzer != null && (string.IsNullOrEmpty(completions._expr) || pyReplEval.Analyzer.ShouldEvaluateForCompletion(completions._expr))) {
                var replMembers = pyReplEval.GetMemberNames(completions._expr ?? "");

                if (_services.Python.InteractiveOptions.LiveCompletionsOnly) {
                    members = replMembers ?? Array.Empty<CompletionResult>();
                } else if (replMembers != null) {
                    members = members.Union(replMembers, CompletionMergeKeyComparer.Instance);
                }
            }

            if (pyReplEval == null && (completions._allowSnippet ?? false)) {
                var expansions = analyzer.WaitForRequest(_services.Python?.GetExpansionCompletionsAsync(), "GetCompletions.GetExpansions", null, 5);
                if (expansions != null) {
                    // Expansions should come first, so that they replace our keyword
                    // completions with the more detailed snippets.
                    members = expansions.Union(members, CompletionMergeKeyComparer.Instance);
                }
            }


            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start) > TooMuchTime) {
                var memberArray = members.ToArray();
                members = memberArray;
                Trace.WriteLine($"{this} lookup time {end - start} for {memberArray.Length} members");
            }

            start = _stopwatch.ElapsedMilliseconds;

            var result = new FuzzyCompletionSet(
                "Python",
                "Python",
                _span,
                members.Select(m => PythonCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast,
                matchInsertionText: true
            ) {
                CommitByDefault = completions._commitByDefault ?? true
            };


            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start) > TooMuchTime) {
                Trace.WriteLine($"{this} completion set time {end - start} total time {end - start}");
            }

            return result;
        }

        internal DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, CompletionResult memberResult) {
            var insert = memberResult.Completion;
            if (insert.IndexOf('\t') >= 0 && _view.Options.IsConvertTabsToSpacesEnabled()) {
                insert = insert.Replace("\t", new string(' ', _view.Options.GetIndentSize()));
            }

            return new DynamicallyVisibleCompletion(memberResult.Name, 
                insert,
                () => memberResult.Documentation, 
                () => service.GetGlyph(memberResult.MemberType.ToGlyphGroup(), StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(PythonMemberType), memberResult.MemberType)
            );
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, string name, string tooltip, StandardGlyphGroup group) {
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new DynamicallyVisibleCompletion(name, 
                name, 
                tooltip, 
                service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(StandardGlyphGroup), group));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, string name, string completion, string tooltip, StandardGlyphGroup group) {
            var icon = new IconDescription(group, StandardGlyphItem.GlyphItemPublic);

            var result = new DynamicallyVisibleCompletion(name, 
                completion, 
                tooltip, 
                service.GetGlyph(group, StandardGlyphItem.GlyphItemPublic),
                Enum.GetName(typeof(StandardGlyphGroup), group));
            result.Properties.AddProperty(typeof(IconDescription), icon);
            return result;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        public override string ToString() {
            if (_span == null) {
                return "CompletionContext.EmptyCompletionContext";
            }
            var snapSpan = _span.GetSpan(_textBuffer.CurrentSnapshot);
            return String.Format("CompletionContext({0}): {1} @{2}", GetType().Name, snapSpan.GetText(), snapSpan.Span);
        }
    }
}

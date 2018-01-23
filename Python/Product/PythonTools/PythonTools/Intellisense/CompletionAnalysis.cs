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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides various completion services after the text around the current location has been
    /// processed. The completion services are specific to the current context
    /// </summary>
    class CompletionAnalysis {
        private readonly PythonEditorServices _services;
        private readonly ICompletionSession _session;
        private readonly ITextView _view;
        private readonly ITrackingSpan _span;
        private readonly ITextBuffer _textBuffer;
        protected readonly CompletionOptions _options;
        internal const Int64 TooMuchTime = 50;
        protected static Stopwatch _stopwatch = MakeStopWatch();

        internal static CompletionAnalysis EmptyCompletionContext = new CompletionAnalysis(null, null, null, null, null, null);

        internal CompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
            _session = session;
            _view = view;
            _span = span;
            _services = services;
            _textBuffer = textBuffer;
            _options = (options == null) ? new CompletionOptions() : options.Clone();
        }

        internal PythonEditorServices EditorServices => _services;
        public ICompletionSession Session => _session;
        public ITextBuffer TextBuffer => _textBuffer;
        public ITrackingSpan Span => _span;
        public ITextView View => _view;

        public virtual CompletionSet GetCompletions(IGlyphService glyphService) {
            return null;
        }

        internal static bool IsKeyword(ClassificationSpan token, string keyword, Lazy<string> text = null) {
            return token.ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && (text?.Value ?? token.Span.GetText()) == keyword;
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, CompletionResult memberResult) {
            return new DynamicallyVisibleCompletion(memberResult.Name, 
                memberResult.Completion, 
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

        internal PythonTextBufferInfo GetBufferInfo() {
            var bi = PythonTextBufferInfo.TryGetForBuffer(TextBuffer);
            Debug.Assert(bi != null, "Getting completions from uninitialized buffer " + TextBuffer.ToString());
            return bi;
        }

        internal AnalysisEntry GetAnalysisEntry() {
            return GetBufferInfo()?.AnalysisEntry;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        protected IEnumerable<CompletionResult> GetModules(string[] package, bool modulesOnly = true) {
            var analysis = GetAnalysisEntry();
            if (analysis == null) {
                return Enumerable.Empty<CompletionResult>();
            }

            IPythonInteractiveIntellisense pyReplEval = null;
            IInteractiveEvaluator eval;
            if (TextBuffer.Properties.TryGetProperty(typeof(IInteractiveEvaluator), out eval)) {
                pyReplEval = eval as IPythonInteractiveIntellisense;
            }
            IEnumerable<KeyValuePair<string, string>> replScopes = null;
            if (pyReplEval != null) {
                replScopes = pyReplEval.GetAvailableScopesAndPaths();
            }

            if (package == null) {
                package = new string[0];
            }

            var modules = Enumerable.Empty<CompletionResult>();

            if (analysis != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                var analyzer = analysis.Analyzer;
                IEnumerable<CompletionResult> result;

                if (modulesOnly || package.Length == 0) {
                    result = analyzer.WaitForRequest(analyzer.GetModulesAsync(analysis, package), "GetModules");
                } else {
                    result = analyzer.WaitForRequest(analyzer.GetMembersAsync(
                        analysis,
                        $"__import__('{string.Join(".", package)}')",
                        SourceLocation.MinValue,
                        Analysis.GetMemberOptions.None
                    ), "GetMembers");
                }

                if (result != null) {
                    modules = modules.Concat(result).Distinct(CompletionComparer.MemberEquality);
                }
            }

            if (replScopes != null) {
                modules = GetModulesFromReplScope(replScopes, package)
                    .Concat(modules)
                    .Distinct(CompletionComparer.MemberEquality);
            }

            return modules;
        }

        private static IEnumerable<CompletionResult> GetModulesFromReplScope(
            IEnumerable<KeyValuePair<string, string>> scopes,
            string[] package
        ) {
            if (package == null || package.Length == 0) {
                foreach (var scope in scopes) {
                    if (scope.Key.IndexOf('.') < 0) {
                        yield return new CompletionResult(
                            scope.Key,
                            string.IsNullOrEmpty(scope.Value) ? PythonMemberType.Namespace : PythonMemberType.Module
                        );
                    }
                }
            } else {
                foreach (var scope in scopes) {
                    var parts = scope.Key.Split('.');
                    if (parts.Length - 1 == package.Length &&
                        parts.Take(parts.Length - 1).SequenceEqual(package, StringComparer.Ordinal)) {
                        yield return new CompletionResult(
                            parts[parts.Length - 1],
                            string.IsNullOrEmpty(scope.Value) ? PythonMemberType.Namespace : PythonMemberType.Module
                        );
                    }
                }
            }
        }

        public override string ToString() {
            if (Span == null) {
                return "CompletionContext.EmptyCompletionContext";
            };
            var snapSpan = Span.GetSpan(TextBuffer.CurrentSnapshot);
            return String.Format("CompletionContext({0}): {1} @{2}", GetType().Name, snapSpan.GetText(), snapSpan.Span);
        }
    }
}

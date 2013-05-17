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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
#endif

    /// <summary>
    /// Provides various completion services after the text around the current location has been
    /// processed. The completion services are specific to the current context
    /// </summary>
    public class CompletionAnalysis {
        private readonly ITrackingSpan _span;
        private readonly ITextBuffer _textBuffer;
        internal readonly CompletionOptions _options;
        internal const Int64 TooMuchTime = 50;
        protected static Stopwatch _stopwatch = MakeStopWatch();

        internal static CompletionAnalysis EmptyCompletionContext = new CompletionAnalysis(null, null, null);

        internal CompletionAnalysis(ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
            _span = span;
            _textBuffer = textBuffer;
            _options = (options == null) ? new CompletionOptions() : options.Clone();
        }

        public ITextBuffer TextBuffer {
            get {
                return _textBuffer;
            }
        }

        public ITrackingSpan Span {
            get {
                return _span;
            }
        }

        public virtual CompletionSet GetCompletions(IGlyphService glyphService) {
            return null;
        }

        internal static bool IsKeyword(ClassificationSpan token, string keyword) {
            return token.ClassificationType.Classification == PredefinedClassificationTypeNames.Keyword && token.Span.GetText() == keyword;
        }

        internal static DynamicallyVisibleCompletion PythonCompletion(IGlyphService service, MemberResult memberResult) {
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

        internal ModuleAnalysis GetAnalysisEntry() {
            return ((IPythonProjectEntry)TextBuffer.GetAnalysis()).Analysis;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        protected IEnumerable<MemberResult> GetModules(string[] package, bool modulesOnly = true) {
            var analysis = GetAnalysisEntry();

            IPythonReplIntellisense pyReplEval = null;
            IReplEvaluator eval;
            if (TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval)) {
                pyReplEval = eval as IPythonReplIntellisense;
            }
            IEnumerable<KeyValuePair<string, bool>> replScopes = null;
            if (pyReplEval != null) {
                replScopes = pyReplEval.GetAvailableScopesAndKind();
            }

            if (package == null) {
                package = new string[0];
            }

            var modules = Enumerable.Empty<MemberResult>();
            if (analysis != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                modules = modules.Concat((package != null && package.Length > 0) ? 
                    analysis.GetModuleMembers(package, !modulesOnly) : 
                    analysis.GetModules(true));
            }
            if (replScopes != null) {
                modules = replScopes
                    .Select(scope => new MemberResult(scope.Key, scope.Value ? PythonMemberType.Module : PythonMemberType.Namespace))
                    .Union(modules);
            }

            return modules;
        }

        public DynamicallyVisibleCompletion[] GetModules(IGlyphService glyphService, string text, bool includeMembers = false) {
            var analysis = GetAnalysisEntry();
            var path = text.Split('.');
            if (path.Length > 0) {
                // path = path[:-1]
                var newPath = new string[path.Length - 1];
                Array.Copy(path, newPath, path.Length - 1);
                path = newPath;
            }

            IPythonReplIntellisense pyReplEval = null;
            IReplEvaluator eval;
            if (TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval)) {
                pyReplEval = eval as IPythonReplIntellisense;
            }
            IEnumerable<KeyValuePair<string, bool>> replScopes = null;
            if (pyReplEval != null) {
                replScopes = pyReplEval.GetAvailableScopesAndKind();
            }

            MemberResult[] modules = new MemberResult[0];
            if (path.Length == 0) {
                if (analysis != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                    modules = analysis.GetModules(true);
                }
                if (replScopes != null) {
                    HashSet<MemberResult> allModules = new HashSet<MemberResult>(CompletionComparer.UnderscoresLast);
                    allModules.UnionWith(modules);
                    foreach (var scope in replScopes) {
                        // remove an existing scope, add the new one (we take precedence)
                        var newMod = new MemberResult(scope.Key, scope.Value ? PythonMemberType.Module : PythonMemberType.Namespace);
                        allModules.Remove(newMod);
                        allModules.Add(newMod);
                    }
                    modules = allModules.ToArray();
                }
            } else {
                if (analysis != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                    modules = analysis.GetModuleMembers(path, includeMembers);
                }

                if (replScopes != null) {
                    HashSet<MemberResult> allModules = new HashSet<MemberResult>(CompletionComparer.UnderscoresLast);
                    allModules.UnionWith(modules);
                    foreach (var scopeAndKind in replScopes) {
                        var scope = scopeAndKind.Key;
                        var isModule = scopeAndKind.Value;

                        if (scope.StartsWith(text, StringComparison.OrdinalIgnoreCase)) {
                            var split = scope.Split('.');
                            var subPath = new string[split.Length - path.Length];
                            for(int i = 0; i<subPath.Length; i++) {
                                subPath[i] = split[i + path.Length];
                            }
                            
                            // remove an existing scope, add the new one (we take precedence)
                            var newMod = new MemberResult(String.Join(".", subPath), isModule ? PythonMemberType.Module : PythonMemberType.Namespace);
                            allModules.Remove(newMod);
                            allModules.Add(newMod);
                        }
                    }
                    modules = allModules.ToArray();
                }
            }

            Array.Sort(modules, CompletionComparer.UnderscoresLast);
            return modules.Select(m => PythonCompletion(glyphService, m)).ToArray();
        }

        public override string ToString() {
            var snapSpan = Span.GetSpan(TextBuffer.CurrentSnapshot);
            return String.Format("CompletionContext({0}): {1} @{2}", GetType().Name, snapSpan.GetText(), snapSpan.Span);
        }
    }
}

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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.PatternMatching;
using AP = Microsoft.PythonTools.Intellisense.AnalysisProtocol;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    internal class PythonNavigateToItemProvider : INavigateToItemProvider {
        private readonly PythonEditorServices _services;
        private readonly AnalyzerInfo[] _analyzers;
        private CancellationTokenSource _searchCts;

        // Used to propagate information to PythonNavigateToItemDisplay inside NavigateToItem.Tag.
        internal class ItemTag {
            public PythonEditorServices Services { get; set; }
            public AP.Completion Completion { get; set; }
            public string ProjectName { get; set; }
        }

        private class AnalyzerInfo {
            public string ProjectName;
            public VsProjectAnalyzer Analyzer;
            public Task<AP.CompletionsResponse> Task;
            public AP.CompletionsResponse Result;
        }

        public PythonNavigateToItemProvider(PythonEditorServices services) {
            _services = services;
            var solution = (IVsSolution)_services.Site.GetService(typeof(SVsSolution));
            _analyzers = _services.Python.GetActiveAnalyzers()
                .Select(p => new AnalyzerInfo { Analyzer = p.Value, ProjectName = p.Key })
                .Where(a => a.Analyzer != null)
                .ToArray();
        }

        public async void StartSearch(INavigateToCallback callback, string searchValue) {
            CancellationTokenSource searchCts = null;

            try {
                searchCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _searchCts, searchCts);
                try {
                    oldCts?.Cancel();
                    oldCts?.Dispose();
                } catch (ObjectDisposedException) {
                }

                CancellationToken token;
                try {
                    token = searchCts.Token;
                } catch (ObjectDisposedException) {
                    // highly unlikely race, but easy enough to protect against
                    return;
                }

                await FindMatchesAsync(callback, searchValue, token);
            } catch (OperationCanceledException) {
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(_services.Site, GetType());
            } finally {
                callback.Done();
                if (searchCts != null) {
                    Interlocked.CompareExchange(ref _searchCts, null, searchCts);
                    searchCts.Dispose();
                }
            }
        }

        private async Task FindMatchesAsync(INavigateToCallback callback, string searchValue, CancellationToken token) {
#if USE_15_5
            foreach (var a in _analyzers) {
                a.Task = null;
                a.Result = null;
            }
            callback.Done();
        }
#else
            var matcher = _services.PatternMatcherFactory.CreatePatternMatcher(
                searchValue,
                new PatternMatcherCreationOptions(CultureInfo.CurrentUICulture, PatternMatcherCreationFlags.AllowFuzzyMatching | PatternMatcherCreationFlags.AllowSimpleSubstringMatching)
            );

            var opts = GetMemberOptions.NoMemberRecursion | GetMemberOptions.IntersectMultipleResults | GetMemberOptions.DetailedInformation;
            foreach (var a in _analyzers) {
                a.Task = a.Analyzer.SendRequestAsync(new AP.GetAllMembersRequest { options = opts });
            }
            foreach (var a in _analyzers) {
                a.Result = await a.Task;
            }
            token.ThrowIfCancellationRequested();

            int progress = 0;
            int total = _analyzers.Sum(r => r.Result?.completions?.Length ?? 0);
            foreach (var a in _analyzers) {
                token.ThrowIfCancellationRequested();
                if ((a.Result?.completions?.Length ?? 0) == 0) {
                    continue;
                }

                foreach (var res in FilterResults(a.ProjectName, a.Result.completions, matcher)) {
                    callback.AddItem(res);
                }

                progress += a.Result.completions.Length;
                callback.ReportProgress(progress, total);
            }
        }

        private IEnumerable<NavigateToItem> FilterResults(
            string projectName,
            IEnumerable<AP.Completion> completions,
            IPatternMatcher matcher
        ) {
            foreach (var c in completions) {
                var match = matcher.TryMatch(c.name);
                if (match == null) {
                    continue;
                }

                var itemTag = new ItemTag {
                    Services = _services,
                    Completion = c,
                    ProjectName = projectName
                };

                yield return new NavigateToItem(
                    c.name,
                    c.memberType.ToString(),
                    "Python",
                    "",
                    itemTag,
                    match.Value,
                    PythonNavigateToItemDisplayFactory.Instance
                );
            }
        }
#endif

        public void StopSearch() {
            try {
                Volatile.Read(ref _searchCts)?.Cancel();
            } catch (ObjectDisposedException) {
            }
        }

        public void Dispose() {
            var cts = Interlocked.Exchange(ref _searchCts, null);
            try {
                cts?.Cancel();
                cts?.Dispose();
            } catch (ObjectDisposedException) {
            }
        }
    }
}

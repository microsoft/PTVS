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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using AP = Microsoft.PythonTools.Intellisense.AnalysisProtocol;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    internal class PythonNavigateToItemProvider : INavigateToItemProvider {
        private readonly IServiceProvider _serviceProvider;
        private readonly AnalyzerInfo[] _analyzers;
        private readonly FuzzyMatchMode _matchMode;
        private readonly IGlyphService _glyphService;
        private CancellationTokenSource _searchCts;

        // Used to propagate information to PythonNavigateToItemDisplay inside NavigateToItem.Tag.
        internal class ItemTag {
            public IServiceProvider Site { get; set; }
            public IGlyphService GlyphService { get; set; }
            public AP.Completion Completion { get; set; }
            public string ProjectName { get; set; }
        }

        private class AnalyzerInfo {
            public string ProjectName;
            public VsProjectAnalyzer Analyzer;
            public Task<AP.CompletionsResponse> Task;
            public AP.CompletionsResponse Result;
        }

        public PythonNavigateToItemProvider(IServiceProvider serviceProvider, IGlyphService glyphService) {
            _serviceProvider = serviceProvider;
            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
            _analyzers = solution.EnumerateLoadedPythonProjects()
                .Select(p => new AnalyzerInfo { Analyzer = p.GetAnalyzer(), ProjectName = p.Caption })
                .Where(a => a.Analyzer != null)
                .ToArray();
            _glyphService = glyphService;
            var pyService = _serviceProvider.GetPythonToolsService();
            _matchMode = pyService?.AdvancedOptions.SearchMode ?? FuzzyMatchMode.FuzzyIgnoreLowerCase;
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
                ex.ReportUnhandledException(_serviceProvider, GetType());
            } finally {
                callback.Done();
                if (searchCts != null) {
                    Interlocked.CompareExchange(ref _searchCts, null, searchCts);
                    searchCts.Dispose();
                }
            }
        }

#pragma warning disable 618 // TODO: deal with 15.6 MatchKind deprecation later

        private async Task FindMatchesAsync(INavigateToCallback callback, string searchValue, CancellationToken token) {
            var matchers = new List<Tuple<FuzzyStringMatcher, string, MatchKind>> {
                    Tuple.Create(new FuzzyStringMatcher(FuzzyMatchMode.Prefix), searchValue, MatchKind.Prefix),
                    Tuple.Create(new FuzzyStringMatcher(_matchMode), searchValue, MatchKind.Regular)
                };

            if (searchValue.Length > 2 && searchValue.StartsWith("/") && searchValue.EndsWith("/")) {
                matchers.Insert(0, Tuple.Create(
                    new FuzzyStringMatcher(FuzzyMatchMode.RegexIgnoreCase),
                    searchValue.Substring(1, searchValue.Length - 2),
                    MatchKind.Regular
                ));
            }

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

                foreach (var res in FilterResults(a.ProjectName, a.Result.completions, matchers)) {
                    callback.AddItem(res);
                }

                progress += a.Result.completions.Length;
                callback.ReportProgress(progress, total);
            }
        }
        private IEnumerable<NavigateToItem> FilterResults(
            string projectName,
            IEnumerable<AP.Completion> completions,
            IEnumerable<Tuple<FuzzyStringMatcher, string, MatchKind>> matchers
        ) {
            foreach (var c in completions) {
                MatchKind matchKind = MatchKind.None;
                foreach (var m in matchers) {
                    if (m.Item1.IsCandidateMatch(c.name, m.Item2)) {
                        matchKind = m.Item3;
                        break;
                    }
                }
                if (matchKind == MatchKind.None) {
                    continue;
                }

                var itemTag = new ItemTag {
                    Site = _serviceProvider,
                    Completion = c,
                    GlyphService = _glyphService,
                    ProjectName = projectName
                };

                yield return new NavigateToItem(
                    c.name,
                    c.memberType.ToString(),
                    "Python",
                    "",
                    itemTag,
                    matchKind,
                    PythonNavigateToItemDisplayFactory.Instance
                );
            }
        }

#pragma warning restore 618

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

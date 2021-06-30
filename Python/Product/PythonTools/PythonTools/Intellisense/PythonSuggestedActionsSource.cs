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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    class PythonSuggestedActionsSource : ISuggestedActionsSource, IPythonTextBufferInfoEventSink {
        internal readonly PythonEditorServices _services;

        private readonly object _currentLock = new object();
        private IEnumerable<SuggestedActionSet> _current;
        private SnapshotSpan _currentSpan;
        private readonly UIThreadBase _uiThread;

        private static readonly Guid _telemetryId = new Guid("{9D2182D9-27BC-4143-9A93-B7D9C015D01B}");

        public PythonSuggestedActionsSource(PythonEditorServices services) {
            _services = services;
            _uiThread = _services.Site.GetUIThread();
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() { }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            lock (_currentLock) {
                if (_currentSpan == range) {
                    return _current;
                }
            }
            return null;
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            var textBuffer = range.Snapshot.TextBuffer;
            var bi = _services.GetBufferInfo(textBuffer);
            var entry = bi.AnalysisEntry;
            if (entry == null) {
                return false;
            }

            if (entry != null) {
                return false;
            }

            var needSuggestion = new List<SnapshotSpan>();

            var tokens = bi.GetTokens(range).Where(t => t.Category == TokenCategory.Identifier);
            foreach (var t in tokens) {
                var span = t.ToSnapshotSpan(range.Snapshot);
                var isMissing = await entry.Analyzer.IsMissingImportAsync(
                    entry,
                    span.GetText(),
                    t.ToSourceSpan().Start
                );

                if (isMissing) {
                    needSuggestion.Add(span);
                }
            }

            if (!needSuggestion.Any()) {
                return false;
            }


            var suggestions = new List<SuggestedActionSet>();

            foreach (var span in needSuggestion) {
                var available = await entry.Analyzer.FindNameInAllModulesAsync(span.GetText());
                var actions = available.Select(s => new PythonSuggestedImportAction(this, textBuffer, s))
                    .OrderBy(k => k)
                    .Distinct()
                    .ToArray();
                if (actions.Any()) {
                    suggestions.Add(new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.CodeFix, actions));
                }
            }

            if (!suggestions.Any()) {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (_currentLock) {
                cancellationToken.ThrowIfCancellationRequested();
                _current = suggestions;
                _currentSpan = range;
            }

            return true;
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            telemetryId = _telemetryId;
            return false;
        }

        Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            if (e.Event == PythonTextBufferInfoEvents.NewAnalysis) {
                SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
            }
            return Task.CompletedTask;
        }
    }
}

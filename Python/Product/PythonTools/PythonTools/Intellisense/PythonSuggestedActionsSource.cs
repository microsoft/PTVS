using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using System.Threading;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
#if DEV14_OR_LATER
    class PythonSuggestedActionsSource : ISuggestedActionsSource {
        internal readonly IServiceProvider _provider;
        internal readonly ITextView _view;
        internal readonly ITextBuffer _textBuffer;

        private readonly object _currentLock = new object();
        private IEnumerable<SuggestedActionSet> _current;
        private SnapshotSpan _currentSpan;

        public PythonSuggestedActionsSource(
            IServiceProvider provider,
            ITextView textView,
            ITextBuffer textBuffer
        ) {
            _provider = provider;
            _view = textView;
            _textBuffer = textBuffer;
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged {
            add { }
            remove { }
        }

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
            var textBuffer = _textBuffer;
            var span = range.Snapshot.CreateTrackingSpan(range, SpanTrackingMode.EdgeInclusive);
            var imports = textBuffer.CurrentSnapshot.GetMissingImports(_provider, span);

            if (imports == MissingImportAnalysis.Empty) {
                return false;
            }

            var suggestions = new List<SuggestedActionSet>();
            var availableImports = await imports.GetAvailableImportsAsync(cancellationToken);

            suggestions.Add(new SuggestedActionSet(
                availableImports.Select(s => new PythonSuggestedImportAction(this, s)).OrderBy(k => k)
            ));

            cancellationToken.ThrowIfCancellationRequested();
            lock (_currentLock) {
                cancellationToken.ThrowIfCancellationRequested();
                _current = suggestions;
                _currentSpan = range;
            }

            return true;
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            telemetryId = Guid.Empty;
            return false;
        }
    }
#endif
}

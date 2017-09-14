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
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Intellisense {
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Python Extract Method Suggested Action")]
    [ContentType(PythonCoreConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class ExtractMethodSuggestedActionSourceProvider : ISuggestedActionsSourceProvider {
        private readonly IServiceProvider _serviceProvider;
        private readonly Lazy<PreviewChangesService> _changePreviewFactory;
        private readonly Lazy<AnalysisEntryService> _entryService;

        [ImportingConstructor]
        public ExtractMethodSuggestedActionSourceProvider([Import(typeof(SVsServiceProvider))] IServiceProvider provider, Lazy<PreviewChangesService> changePreviewFactory, Lazy<AnalysisEntryService> entryService) {
            _serviceProvider = provider;
            _changePreviewFactory = changePreviewFactory;
            _entryService = entryService;
        }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
            return new ExtractMethodSuggestedActionSource(this, textView);
        }

        class ExtractMethodSuggestedActionSource : ISuggestedActionsSource {
            private readonly ITextView _view;
            private readonly ExtractMethodSuggestedActionSourceProvider _parent;
            private bool? _canExtract;
            private static readonly Guid _telemetryId = new Guid("{0D887D91-1E11-4DC3-92F5-79A5182004C3}");

            public ExtractMethodSuggestedActionSource(ExtractMethodSuggestedActionSourceProvider parent, ITextView view) {
                _view = view;
                _parent = parent;
                _view.Selection.SelectionChanged += Selection_SelectionChanged;
            }

            private void Selection_SelectionChanged(object sender, EventArgs e) {
                var newValue = MethodExtractor.CanExtract(_view);
                if (newValue != _canExtract) {
                    _canExtract = newValue;
                    SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public event EventHandler<EventArgs> SuggestedActionsChanged;

            public void Dispose() {
                _view.Selection.SelectionChanged -= Selection_SelectionChanged;
            }

            public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
                if (MethodExtractor.CanExtract(_view) == true) {
                    return new SuggestedActionSet[] {
                    new SuggestedActionSet(
                        new [] {  new SuggestedAction(_parent, _view) },
                        SuggestedActionSetPriority.High
                    )
                };
                }

                return Enumerable.Empty<SuggestedActionSet>();
            }

            public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
                if (MethodExtractor.CanExtract(_view) == true) {
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }

            public bool TryGetTelemetryId(out Guid telemetryId) {
                telemetryId = _telemetryId;
                return true;
            }

            class SuggestedAction : ISuggestedAction {
                private readonly ExtractMethodSuggestedActionSourceProvider _parent;
                private readonly ITextView _view;

                public SuggestedAction(ExtractMethodSuggestedActionSourceProvider parent, ITextView view) {
                    _parent = parent;
                    _view = view;
                }

                public string DisplayText {
                    get {
                        return Strings.ExtractMethod;
                    }
                }

                public bool HasActionSets {
                    get {
                        return false;
                    }
                }

                public bool HasPreview {
                    get {
                        return true;
                    }
                }

                public string IconAutomationText {
                    get {
                        return null;
                    }
                }

                public ImageMoniker IconMoniker {
                    get {
                        return default(ImageMoniker);
                    }
                }

                public string InputGestureText {
                    get {
                        return null;
                    }
                }

                public void Dispose() {
                }

                public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
                    return Task.FromResult(Enumerable.Empty<SuggestedActionSet>());
                }

                public async Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
                    AnalysisEntry entry;
                    if (_parent._entryService.Value == null || !_parent._entryService.Value.TryGetAnalysisEntry(_view, out entry)) {
                        return null;
                    }

                    var extractInfo = await entry.Analyzer.ExtractMethodAsync(
                        entry,
                        _view.TextBuffer,
                        _view,
                        "new_method",
                        null,
                        null
                    );
                    if (extractInfo == null || extractInfo.Data == null) {
                        return null;
                    }

                    var changes = extractInfo.Data.changes;
                    var tracker = extractInfo.GetTracker(extractInfo.Data.version);
                    var originalBuffer = _view.TextBuffer;
                    if (changes == null || tracker == null || originalBuffer == null) {
                        return null;
                    }

                    return _parent._changePreviewFactory.Value.CreateDiffView(
                        changes,
                        tracker,
                        originalBuffer
                    );
                }


                public void Invoke(CancellationToken cancellationToken) {
                    new MethodExtractor(
                        _parent._serviceProvider,
                        _view
                    ).ExtractMethod(
                        new ExtractMethodUserInput(_parent._serviceProvider)
                    ).DoNotWait();
                }

                public bool TryGetTelemetryId(out Guid telemetryId) {
                    telemetryId = _telemetryId;
                    return true;
                }
            }
        }

    }
}

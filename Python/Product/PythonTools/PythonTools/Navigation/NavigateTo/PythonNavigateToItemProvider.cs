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
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Navigation;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    internal class PythonNavigateToItemProvider : INavigateToItemProvider {
        private readonly IServiceProvider _serviceProvider;
        private readonly Library _library;
        private readonly FuzzyMatchMode _matchMode;
        private readonly IGlyphService _glyphService;
        private CancellationTokenSource _searchCts;

        // Used to propagate information to PythonNavigateToItemDisplay inside NavigateToItem.Tag.
        internal class ItemTag {
            public LibraryNode Node { get; set; }
            public IGlyphService GlyphService { get; set; }
        }

        private class LibraryNodeVisitor : ILibraryNodeVisitor {
            private static readonly Dictionary<StandardGlyphGroup, string> _sggToNavItemKind = new Dictionary<StandardGlyphGroup, string>() {
                { StandardGlyphGroup.GlyphGroupClass, NavigateToItemKind.Class },
                { StandardGlyphGroup.GlyphGroupMethod, NavigateToItemKind.Method },
                { StandardGlyphGroup.GlyphGroupField, NavigateToItemKind.Field }
            };

            private readonly PythonNavigateToItemProvider _itemProvider;
            private readonly INavigateToCallback _navCallback;
            private readonly string _searchValue;
            private readonly Stack<LibraryNode> _path = new Stack<LibraryNode>();
            private readonly FuzzyStringMatcher _comparer, _regexComparer;

            private static readonly Guid _projectType = new Guid(PythonConstants.ProjectFactoryGuid);

            public LibraryNodeVisitor(
                PythonNavigateToItemProvider itemProvider,
                INavigateToCallback navCallback,
                string searchValue,
                FuzzyMatchMode matchMode
            ) {
                _itemProvider = itemProvider;
                _navCallback = navCallback;
                _searchValue = searchValue;
                _path.Push(null);
                _comparer = new FuzzyStringMatcher(matchMode);
                _regexComparer = new FuzzyStringMatcher(FuzzyMatchMode.RegexIgnoreCase);
            }

            public bool EnterNode(LibraryNode node, CancellationToken ct) {
                if (ct.IsCancellationRequested) {
                    _navCallback.Invalidate();
                    return false;
                }

                IVsHierarchy hierarchy;
                uint itemId, itemsCount;
                Guid projectType;
                node.SourceItems(out hierarchy, out itemId, out itemsCount);
                if (hierarchy != null) {
                    ErrorHandler.ThrowOnFailure(hierarchy.GetGuidProperty(
                        (uint)VSConstants.VSITEMID.Root,
                        (int)__VSHPROPID.VSHPROPID_TypeGuid,
                        out projectType
                    ));
                    if (projectType != _projectType) {
                        return false;
                    }
                }

                var parentNode = _path.Peek();
                _path.Push(node);

                // We don't want to report modules, since they map 1-to-1 to files, and those are already reported by the standard item provider
                if (node.NodeType.HasFlag(LibraryNodeType.Package)) {
                    return true;
                }

                // Match name against search string.
                string name = node.Name ?? "";
                MatchKind matchKind;
                if (_searchValue.Length > 2 && _searchValue.StartsWith("/") && _searchValue.EndsWith("/")) {
                    if (!_regexComparer.IsCandidateMatch(name, _searchValue.Substring(1, _searchValue.Length - 2))) {
                        return true;
                    }
                    matchKind = MatchKind.Regular;
                } else if (name.Equals(_searchValue, StringComparison.Ordinal)) {
                    matchKind = MatchKind.Exact;
                } else if (_comparer.IsCandidateMatch(name, _searchValue)) {
                    matchKind = MatchKind.Regular;
                } else {
                    return true;
                }

                string kind;
                if (!_sggToNavItemKind.TryGetValue(node.GlyphType, out kind)) {
                    kind = "";
                }
                
                var text = node.GetTextRepresentation(VSTREETEXTOPTIONS.TTO_DISPLAYTEXT);
                if (parentNode != null) {
                    switch (parentNode.GlyphType) {
                        case StandardGlyphGroup.GlyphGroupModule:
                            text += string.Format(" [of module {0}]", parentNode.Name);
                            break;
                        case StandardGlyphGroup.GlyphGroupClass:
                            text += string.Format(" [of class {0}]", parentNode.Name);
                            break;
                        case StandardGlyphGroup.GlyphGroupMethod:
                            text += string.Format(" [nested in function {0}]", parentNode.Name);
                            break;
                    }
                }

                var tag = new ItemTag { Node = node, GlyphService = _itemProvider._glyphService };
                _navCallback.AddItem(new NavigateToItem(text, kind, "Python", "", tag, matchKind, PythonNavigateToItemDisplayFactory.Instance));
                return true;
            }

            public void LeaveNode(LibraryNode node, CancellationToken ct) {
                _path.Pop();
            }
        }

        public PythonNavigateToItemProvider(IServiceProvider serviceProvider, IGlyphService glyphService) {
            _serviceProvider = serviceProvider;
            _glyphService = glyphService;
            var libraryManager = (LibraryManager)_serviceProvider.GetService(typeof(IPythonLibraryManager));
            _library = libraryManager?.Library;
            var pyService = _serviceProvider.GetPythonToolsService();
            _matchMode = pyService?.AdvancedOptions.SearchMode ?? FuzzyMatchMode.FuzzyIgnoreLowerCase;
        }

        public async void StartSearch(INavigateToCallback callback, string searchValue) {
            CancellationTokenSource searchCts;

            if (_library == null) {
                callback.Done();
                return;
            }

            bool success = false;
            try {
                searchCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _searchCts, searchCts);
                if (oldCts != null) {
                    oldCts.Dispose();
                }
                success = true;
            } finally {
                if (!success) {
                    callback.Done();
                }
            }

            try {
                await _library.VisitNodesAsync(
                    new LibraryNodeVisitor(this, callback, searchValue, _matchMode),
                    searchCts.Token
                );
            } catch (OperationCanceledException) {
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(_serviceProvider, GetType());
            } finally {
                callback.Done();
            }
        }

        public void StopSearch() {
            var cts = Volatile.Read(ref _searchCts);
            if (cts != null) {
                cts.Cancel();
            }
        }

        public void Dispose() {
            var cts = Interlocked.Exchange(ref _searchCts, null);
            if (cts != null) {
                cts.Dispose();
            }
        }
    }
}

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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(PythonSnippetManager))]
    internal class PythonSnippetManager {
        private readonly IServiceProvider _site;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IVsTextManager2 _textManager;
        private readonly IVsExpansionManager _vsExpansionMgr;
        private readonly IExpansionManager _expansionMgr;

        private static readonly string[] _allStandardSnippetTypes = { ExpansionClient.Expansion, ExpansionClient.SurroundsWith };
        private static readonly string[] _surroundsWithSnippetTypes = { ExpansionClient.SurroundsWith, ExpansionClient.SurroundsWithStatement };

        [ImportingConstructor]
        public PythonSnippetManager(
            [Import(typeof(SVsServiceProvider))] IServiceProvider site,
            [Import] IVsEditorAdaptersFactoryService editorAdaptersFactoryService
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _editorAdaptersFactoryService = editorAdaptersFactoryService ?? throw new ArgumentNullException(nameof(editorAdaptersFactoryService));
            _textManager = site.GetService<SVsTextManager, IVsTextManager2>();
            _textManager.GetExpansionManager(out _vsExpansionMgr);
            _expansionMgr = _vsExpansionMgr as IExpansionManager;
        }

        public bool IsInSession(ITextView textView) =>
            TryGetExpansionClient(textView)?.InSession ?? false;

        public bool ShowInsertionUI(ITextView textView, bool isSurroundsWith) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            if (_vsExpansionMgr == null) {
                return false;
            }

            string prompt;
            string[] snippetTypes;

            if (isSurroundsWith) {
                prompt = Strings.SurroundWith;
                snippetTypes = _surroundsWithSnippetTypes;
            } else {
                prompt = Strings.InsertSnippet;
                snippetTypes = _allStandardSnippetTypes;
            }

            var client = GetOrCreateExpansionClient(textView);

            var hr = _vsExpansionMgr.InvokeInsertionUI(
                _editorAdaptersFactoryService.GetViewAdapter(textView),
                client,
                CommonGuidList.guidPythonLanguageServiceGuid,
                snippetTypes,
                snippetTypes.Length,
                0,
                null,
                0,
                0,
                prompt,
                ">"
            );

            return ErrorHandler.Succeeded(hr);
        }

        public bool EndSession(ITextView textView, bool leaveCaret) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            var client = TryGetExpansionClient(textView);
            return client != null && ErrorHandler.Succeeded(client.EndCurrentExpansion(leaveCaret));
        }

        public bool MoveToNextField(ITextView textView) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            var client = TryGetExpansionClient(textView);
            return client != null && ErrorHandler.Succeeded(client.NextField());
        }

        public bool MoveToPreviousField(ITextView textView) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            var client = TryGetExpansionClient(textView);
            return client != null && ErrorHandler.Succeeded(client.PreviousField());
        }

        public bool TryTriggerExpansion(ITextView textView) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            if (_vsExpansionMgr == null) {
                return false;
            }

            if (!textView.Selection.IsEmpty || textView.Caret.Position.BufferPosition <= 0) {
                return false;
            }

            var snapshot = textView.TextBuffer.CurrentSnapshot;
            var caretSpan = new SnapshotSpan(snapshot, new Span(textView.Caret.Position.BufferPosition.Position - 1, 1));

            var bufferInfo = PythonTextBufferInfo.ForBuffer(_site, textView.TextBuffer);
            var tokens = bufferInfo.GetTrackingTokens(caretSpan);
            if (!tokens.Any()) {
                return false;
            }

            var token = tokens.First();
            var tokenSpan = token.ToSnapshotSpan(snapshot);
            if (tokenSpan.End.Position != caretSpan.End.Position) {
                // Match C# behavior and only trigger snippet
                // if caret is at the end of an identifier. Otherwise,
                // a TAB should be inserted even if the token matches
                // a snippet shortcut.
                return false;
            }

            var text = tokenSpan.GetText();

            var textSpan = new TextSpan[1];
            textSpan[0].iStartLine = tokenSpan.Start.GetContainingLineNumber();
            textSpan[0].iStartIndex = tokenSpan.Start.Position - tokenSpan.Start.GetContainingLine().Start;
            textSpan[0].iEndLine = tokenSpan.End.GetContainingLineNumber();
            textSpan[0].iEndIndex = tokenSpan.End.Position - tokenSpan.End.GetContainingLine().Start;

            var client = GetOrCreateExpansionClient(textView);
            int hr = _vsExpansionMgr.GetExpansionByShortcut(
                client,
                CommonGuidList.guidPythonLanguageServiceGuid,
                text,
                _editorAdaptersFactoryService.GetViewAdapter(textView),
                textSpan,
                1,
                out string expansionPath,
                out string title
            );

            if (ErrorHandler.Succeeded(hr)) {
                // hr may be S_FALSE if there are multiple expansions,
                // so we don't want to InsertNamedExpansion yet. VS will
                // pop up a selection dialog in this case.
                if (hr == VSConstants.S_OK) {
                    return ErrorHandler.Succeeded(client.InsertNamedExpansion(title, expansionPath, textSpan[0]));
                }
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<VsExpansion>> GetAvailableSnippetsAsync() {
            if (_expansionMgr == null) {
                return Enumerable.Empty<VsExpansion>();
            }

            try {
                var enumerator = await _expansionMgr.EnumerateExpansionsAsync(CommonGuidList.guidPythonLanguageServiceGuid, 1, null, 0, 0, 0);
                if (enumerator == null) {
                    return null;
                }

                var res = new List<VsExpansion>();
                foreach (var e in COMEnumerable.ToList<VsExpansion>(enumerator.Next)) {
                    res.Add(e);
                }

                return res;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                return Enumerable.Empty<VsExpansion>();
            }
        }

        private ExpansionClient GetOrCreateExpansionClient(ITextView textView) {
            if (!textView.Properties.TryGetProperty(typeof(ExpansionClient), out ExpansionClient client)) {
                client = new ExpansionClient(textView, _editorAdaptersFactoryService);
                textView.Properties.AddProperty(typeof(ExpansionClient), client);
            }

            return client;
        }

        private ExpansionClient TryGetExpansionClient(ITextView textView) {
            if (textView.Properties.TryGetProperty(typeof(ExpansionClient), out ExpansionClient client)) {
                return client;
            }

            return null;
        }
    }
}

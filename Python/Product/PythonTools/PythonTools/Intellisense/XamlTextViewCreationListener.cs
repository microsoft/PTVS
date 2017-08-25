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
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Watches for text views to be created for xaml code.  Then wires up to support analysis so that
    /// we can use the analysis for completion in .py code.
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType("xaml")]
    class XamlTextViewCreationListener : IVsTextViewCreationListener {
        private readonly PythonEditorServices _services;

        [ImportingConstructor]
        public XamlTextViewCreationListener(PythonEditorServices services) {
            _services = services;
        }

        public async void VsTextViewCreated(VisualStudio.TextManager.Interop.IVsTextView textViewAdapter) {
            // TODO: We should probably only track text views in Python projects or loose files.
            var textView = _services.EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            
            if (textView != null) {
                var analyzer = _services.AnalysisEntryService.GetVsAnalyzer(textView, null);
                var bi = _services.GetBufferInfo(textView.TextBuffer);
                if (analyzer != null && bi != null && bi.AnalysisEntry == null) {
                    var entry = await analyzer.AnalyzeFileAsync(bi.Filename);
                    if (bi.TrySetAnalysisEntry(entry, null) != entry) {
                        // Failed to start analyzing
                        Debug.Fail("Failed to analyze xaml file");
                        return;
                    }
                    await entry.EnsureCodeSyncedAsync(bi.Buffer);
                    textView.Closed += TextView_Closed;
                }
            }
        }

        private void TextView_Closed(object sender, EventArgs e) {
            var textView = (ITextView)sender;

            //AnalysisEntry entry;
            //if (_entryService.TryGetAnalysisEntry(textView, textView.TextBuffer, out entry)) {
            //    entry.Analyzer.BufferDetached(entry, textView.TextBuffer);
            //}
            
            textView.Closed -= TextView_Closed;
        }
    }
}


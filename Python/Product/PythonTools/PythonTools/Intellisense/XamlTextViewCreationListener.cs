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
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

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
            
            if (textView == null) {
                return;
            }

            var bi = _services.GetBufferInfo(textView.TextBuffer);
            if (bi == null) {
                return;
            }

            var entry = bi.AnalysisEntry ?? await AnalyzeXamlFileAsync(textView, bi);

            for (int retries = 3; retries > 0 && entry == null; --retries) {
                // Likely in the process of changing analyzer, so we'll delay slightly and retry.
                await Task.Delay(100);
                entry = bi.AnalysisEntry ?? await AnalyzeXamlFileAsync(textView, bi);
            }

            if (entry == null) {
                Debug.Fail($"Failed to analyze XAML file {bi.Filename}");
                return;
            }

            if (bi.TrySetAnalysisEntry(entry, null) != entry) {
                // Failed to start analyzing
                Debug.Fail("Failed to analyze xaml file");
                return;
            }
            await entry.EnsureCodeSyncedAsync(bi.Buffer);
        }

        private static async Task<AnalysisEntry> AnalyzeXamlFileAsync(ITextView textView, PythonTextBufferInfo bufferInfo) {
            var services = bufferInfo.Services;
            var analyzer = services.AnalysisEntryService.GetVsAnalyzer(textView, null);
            if (analyzer != null) {
                return await analyzer.AnalyzeFileAsync(bufferInfo.Filename);
            }
            return null;
        }
    }
}


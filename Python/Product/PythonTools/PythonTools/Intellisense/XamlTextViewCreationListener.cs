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

using Microsoft.PythonTools.Editor;
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
        private readonly IServiceProvider _site;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
        private readonly IVsRunningDocumentTable _rdt;
        private PythonEditorServices _services;

        [ImportingConstructor]
        public XamlTextViewCreationListener(
            [Import(typeof(SVsServiceProvider))] IServiceProvider site,
            IVsEditorAdaptersFactoryService editorAdaptersFactory,
            IVsRunningDocumentTable rdt
        ) {
            _site = site;
            _editorAdaptersFactory = editorAdaptersFactory;
            _rdt = rdt;
        }

        public async void VsTextViewCreated(VisualStudio.TextManager.Interop.IVsTextView textViewAdapter) {
            var textView = _editorAdaptersFactory.GetWpfTextView(textViewAdapter);
            if (textView == null) {
                return;
            }

            // Only track text views in Python projects (we don't get called for loose files)
            // For example, we may get called for xaml files in UWP projects, in which case we do nothing
            if (!IsInPythonProject(textView)) {
                return;
            }

            // Load Python services now that we know we'll need them
            if (_services == null) {
                _services = _site.GetComponentModel().GetService<PythonEditorServices>();
                if (_services == null) {
                    return;
                }
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

        private bool IsInPythonProject(IWpfTextView textView) {
            try {
                if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument) && !string.IsNullOrEmpty(textDocument?.FilePath)) {
                    ErrorHandler.ThrowOnFailure(_rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, textDocument.FilePath, out IVsHierarchy hier, out uint itemId, out IntPtr docData, out _));
                    try {
                        ErrorHandler.ThrowOnFailure(hier.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID5.VSHPROPID_ProjectCapabilities, out object propVal));
                        var capabilities = propVal as string;
                        if (capabilities != null && capabilities.Contains("Python")) {
                            return true;
                        }
                    } finally {
                        if (docData != IntPtr.Zero) {
                            Marshal.Release(docData);
                        }
                    }
                }
                return false;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                return false;
            }
        }

        private static async Task<AnalysisEntry> AnalyzeXamlFileAsync(ITextView textView, PythonTextBufferInfo bufferInfo) {
            var services = bufferInfo.Services;

            var analyzer = (await services.Site.FindAnalyzerAsync(textView)) as VsProjectAnalyzer;
            if (analyzer != null) {
                return await analyzer.AnalyzeFileAsync(bufferInfo.Filename);
            }
            return null;
        }
    }
}


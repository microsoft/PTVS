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
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Intellisense {
    [Export(typeof(IIntellisenseControllerProvider)), ContentType(PythonCoreConstants.ContentType), Order]
    [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class IntellisenseControllerProvider : IIntellisenseControllerProvider {
        [Import]
        internal PythonEditorServices Services = null;

        readonly Dictionary<ITextView, Tuple<BufferParser, VsProjectAnalyzer>> _hookedCloseEvents =
            new Dictionary<ITextView, Tuple<BufferParser, VsProjectAnalyzer>>();

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers) {
            if (textView.Roles.Contains("DEBUGVIEW")) {
                // TODO: Determine the context for this view and attach to the correct analyzer
                return null;
            }

            if (textView.TextBuffer.ContentType.IsOfType(CodeRemoteContentDefinition.CodeRemoteContentTypeName)) {
                // We want default handling when this is a remote buffer
                return null;
            }

            if (Services.Python.LanguageServerOptions.ServerDisabled) {
                return null;
            }

            IntellisenseController controller;
            if (!textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                controller = new IntellisenseController(this, textView);
            }

            foreach (var subjectBuffer in subjectBuffers) {
                controller.ConnectSubjectBuffer(subjectBuffer);
            }

            return controller;
        }

        internal static IntellisenseController GetController(ITextView textView) {
            IntellisenseController controller;
            textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller);
            return controller;
        }

        internal static IntellisenseController GetOrCreateController(
            IServiceProvider serviceProvider,
            IComponentModel model,
            ITextView textView
        ) {
            IntellisenseController controller;
            if (!textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                var intellisenseControllerProvider = (
                   from export in model.DefaultExportProvider.GetExports<IIntellisenseControllerProvider, IContentTypeMetadata>()
                   from exportedContentType in export.Metadata.ContentTypes
                   where exportedContentType == PythonCoreConstants.ContentType && export.Value.GetType() == typeof(IntellisenseControllerProvider)
                   select export.Value
                ).First();
                controller = new IntellisenseController((IntellisenseControllerProvider)intellisenseControllerProvider, textView);
            }
            return controller;
        }
    }

    /// <summary>
    /// Monitors creation of text view adapters for Python code so that we can attach
    /// our keyboard filter.  This enables not using a keyboard pre-preprocessor
    /// so we can process all keys for text views which we attach to.  We cannot attach
    /// our command filter on the text view when our intellisense controller is created
    /// because the adapter does not exist.
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(PythonCoreConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class TextViewCreationListener : IVsTextViewCreationListener {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;

        [ImportingConstructor]
        public TextViewCreationListener(IVsEditorAdaptersFactoryService adaptersFactory) {
            _adaptersFactory = adaptersFactory;
        }

        #region IVsTextViewCreationListener Members

        public void VsTextViewCreated(IVsTextView textViewAdapter) {
            var textView = _adaptersFactory.GetWpfTextView(textViewAdapter);
            IntellisenseController controller;
            if (textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                controller.AttachKeyboardFilter();
            }
            InitKeyBindings(textViewAdapter);
        }

        #endregion

        public void InitKeyBindings(IVsTextView vsTextView) {
            var os = vsTextView as IObjectWithSite;
            if (os == null) {
                return;
            }

            IntPtr unkSite = IntPtr.Zero;
            IntPtr unkFrame = IntPtr.Zero;

            try {
                os.GetSite(typeof(VisualStudio.OLE.Interop.IServiceProvider).GUID, out unkSite);
                if (unkSite == IntPtr.Zero) {
                    return;
                }
                var sp = Marshal.GetObjectForIUnknown(unkSite) as VisualStudio.OLE.Interop.IServiceProvider;
                if (sp == null) {
                    return;
                }

                sp.QueryService(typeof(SVsWindowFrame).GUID, typeof(IVsWindowFrame).GUID, out unkFrame);
                if (unkFrame == IntPtr.Zero) {
                    return;
                }

                var frame = Marshal.GetObjectForIUnknown(unkFrame) as IVsWindowFrame;
                if (frame == null) {
                    return;
                }

                frame.SetGuidProperty((int)__VSFPROPID.VSFPROPID_InheritKeyBindings, VSConstants.GUID_TextEditorFactory);
            } finally {
                if (unkSite != IntPtr.Zero) {
                    Marshal.Release(unkSite);
                }
                if (unkFrame != IntPtr.Zero) {
                    Marshal.Release(unkFrame);
                }
            }
        }

    }
}

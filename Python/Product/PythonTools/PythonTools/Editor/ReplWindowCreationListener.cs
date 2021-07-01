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

using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Repl;
using IOleCommandTarget = Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget;

namespace Microsoft.PythonTools.Editor
{
    [Export(typeof(IVsInteractiveWindowOleCommandTargetProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    class ReplWindowCreationListener : IVsInteractiveWindowOleCommandTargetProvider
    {
        private readonly PythonEditorServices _editorServices;

        [ImportingConstructor]
        public ReplWindowCreationListener([Import] PythonEditorServices editorServices)
        {
            _editorServices = editorServices;
        }

        public IOleCommandTarget GetCommandTarget(IWpfTextView textView, IOleCommandTarget nextTarget)
        {
            if (textView.TextBuffer.ContentType.IsOfType(CodeRemoteContentDefinition.CodeRemoteContentTypeName))
            {
                // We want default handling when this is a remote buffer
                return null;
            }

            var window = textView.TextBuffer.GetInteractiveWindow();

            var controller = IntellisenseControllerProvider.GetOrCreateController(
                _editorServices.Site,
                _editorServices.ComponentModel,
                textView
            );
            controller._oldTarget = nextTarget;

            var editFilter = EditFilter.GetOrCreate(_editorServices, textView, controller);

            if (window == null)
            {
                return editFilter;
            }

            textView.Properties[IntellisenseController.SuppressErrorLists] = IntellisenseController.SuppressErrorLists;
            return ReplEditFilter.GetOrCreate(_editorServices.Site, _editorServices.ComponentModel, textView, editFilter);
        }
    }
}

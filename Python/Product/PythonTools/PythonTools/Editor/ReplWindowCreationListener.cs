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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using IOleCommandTarget = Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(IVsInteractiveWindowOleCommandTargetProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    public class PythonOleCommandTargetProvider : IVsInteractiveWindowOleCommandTargetProvider {
        private readonly IEditorOperationsFactoryService _editorOpsFactory;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonOleCommandTargetProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, IEditorOperationsFactoryService editorOpsFactory) {
            _editorOpsFactory = editorOpsFactory;
            _serviceProvider = serviceProvider;
        }

        public IOleCommandTarget GetCommandTarget(IWpfTextView textView, IOleCommandTarget nextTarget) {
            EditFilter filter;
            if (!textView.Properties.TryGetProperty<EditFilter>(typeof(EditFilter), out filter)) {
                textView.Properties[typeof(EditFilter)] = filter = new EditFilter(
                    textView,
                    _editorOpsFactory.GetEditorOperations(textView),
                    _serviceProvider
                );
                var window = textView.TextBuffer.GetInteractiveWindow();
                if (window != null && window.Evaluator is PythonReplEvaluator) {
                    textView.Properties.AddProperty(typeof(PythonReplEvaluator), (PythonReplEvaluator)window.Evaluator);
                }
                var intellisenseController = IntellisenseControllerProvider.GetOrCreateController(
                    _serviceProvider,
                    (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel)),
                    textView
                );
                intellisenseController._oldTarget = nextTarget;
                filter._next = intellisenseController;
            }
            return filter;
        }
    }
}

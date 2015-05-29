/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using IOleCommandTarget = Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget;

#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Repl;
using Microsoft.PythonTools.Repl;
#endif

namespace Microsoft.PythonTools.Editor {
#if DEV14_OR_LATER
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
#else

    [Export(typeof(IReplWindowCreationListener))]
    [ContentType(PythonCoreConstants.ContentType)]
    class ReplWindowCreationListener : IReplWindowCreationListener {
        private readonly IVsEditorAdaptersFactoryService _adapterFact;
        private readonly IEditorOperationsFactoryService _editorOpsFactory;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public ReplWindowCreationListener([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, IVsEditorAdaptersFactoryService adapterFact, IEditorOperationsFactoryService editorOpsFactory) {
            _adapterFact = adapterFact;
            _editorOpsFactory = editorOpsFactory;
            _serviceProvider = serviceProvider;
        }

        public void ReplWindowCreated(IReplWindow window) {
            var model = _serviceProvider.GetComponentModel();
            var textView = window.TextView;
            var vsTextView = _adapterFact.GetViewAdapter(textView);
            if (window.Evaluator is PythonReplEvaluator) {
                textView.Properties.AddProperty(typeof(PythonReplEvaluator), (PythonReplEvaluator)window.Evaluator);
            }

            var editFilter = new EditFilter(window.TextView, _editorOpsFactory.GetEditorOperations(textView), _serviceProvider);
            var intellisenseController = IntellisenseControllerProvider.GetOrCreateController(
                _serviceProvider,
                model,
                textView
            );

            editFilter.AttachKeyboardFilter(vsTextView);
            intellisenseController.AttachKeyboardFilter();
        }
    }
#endif
}

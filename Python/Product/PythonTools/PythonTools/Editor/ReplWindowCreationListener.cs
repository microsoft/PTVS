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

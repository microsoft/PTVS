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

using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplWindowCreationListener = IInteractiveWindowCreationListener;
#endif

    [Export(typeof(IReplWindowCreationListener))]
    [ContentType(PythonCoreConstants.ContentType)]
    class ReplWindowCreationListener : IReplWindowCreationListener {
        private readonly IVsEditorAdaptersFactoryService _adapterFact;
        private readonly IEditorOperationsFactoryService _editorOpsFactory;

        [ImportingConstructor]
        public ReplWindowCreationListener(IVsEditorAdaptersFactoryService adapterFact, IEditorOperationsFactoryService editorOpsFactory) {
            _adapterFact = adapterFact;
            _editorOpsFactory = editorOpsFactory;
        }

        #region IReplWindowCreationListener Members

        public void ReplWindowCreated(IReplWindow window) {
            var model = PythonToolsPackage.ComponentModel;
            var textView = window.TextView;
            var vsTextView = _adapterFact.GetViewAdapter(textView);
            if (window.Evaluator is PythonReplEvaluator) {
                textView.Properties.AddProperty(typeof(PythonReplEvaluator), (PythonReplEvaluator)window.Evaluator);
            }

            var editFilter = new EditFilter(window.TextView, _editorOpsFactory.GetEditorOperations(textView), ServiceProvider.GlobalProvider);
            var intellisenseController = IntellisenseControllerProvider.GetOrCreateController(model, textView);

            editFilter.AttachKeyboardFilter(vsTextView);
            intellisenseController.AttachKeyboardFilter();
        }

        #endregion
    }
}

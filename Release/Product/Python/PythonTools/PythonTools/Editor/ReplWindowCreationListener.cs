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
using Microsoft.PythonTools.Language;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
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
            var textViewAdapter = _adapterFact.GetViewAdapter(window.TextView);
            BraceMatcher.WatchBraceHighlights(window.TextView, PythonToolsPackage.ComponentModel);

            new EditFilter(window.TextView, textViewAdapter, _editorOpsFactory.GetEditorOperations(window.TextView));
        }

        #endregion
    }
}

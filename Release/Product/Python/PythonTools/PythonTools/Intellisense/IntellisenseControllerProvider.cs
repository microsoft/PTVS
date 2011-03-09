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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Intellisense {
    [Export(typeof(IIntellisenseControllerProvider)), ContentType(PythonCoreConstants.ContentType), Order]
    class IntellisenseControllerProvider : IIntellisenseControllerProvider {
        [Import]
        internal ICompletionBroker _CompletionBroker = null; // Set via MEF
        [Import]
        internal IEditorOperationsFactoryService _EditOperationsFactory = null; // Set via MEF
        [Import]
        internal IVsEditorAdaptersFactoryService _adaptersFactory { get; set; }
        [Import]
        internal ISignatureHelpBroker _SigBroker = null; // Set via MEF
        [Import]
        internal IQuickInfoBroker _QuickInfoBroker = null; // Set via MEF
        [Import]
        internal IIncrementalSearchFactoryService _IncrementalSearch = null; // Set via MEF

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers) {
            // Only use the analyzer if the view is actually file backed. If it is the REPL window
            // we don't use this.
            var analyzer = textView.GetAnalyzer();
            if (analyzer != null) {
                var buffer = subjectBuffers[0];

                var entry = analyzer.MonitorTextBuffer(buffer);
                textView.Closed += (sender, args) => analyzer.StopMonitoringTextBuffer(entry.BufferParser);

                for (int i = 1; i < subjectBuffers.Count; i++) {
                    entry.BufferParser.AddBuffer(subjectBuffers[i]);
                }
                return new IntellisenseController(this, subjectBuffers, textView, entry.BufferParser);
            }

            return null;
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
        internal readonly IVsEditorAdaptersFactoryService _adaptersFactory;

        [ImportingConstructor]
        public TextViewCreationListener(IVsEditorAdaptersFactoryService adaptersFactory) {
            _adaptersFactory = adaptersFactory;
        }

        #region IVsTextViewCreationListener Members

        public void VsTextViewCreated(VisualStudio.TextManager.Interop.IVsTextView textViewAdapter) {
            var textView = _adaptersFactory.GetWpfTextView(textViewAdapter);
            IntellisenseController controller;
            if (textView.Properties.TryGetProperty<IntellisenseController>(typeof(IntellisenseController), out controller)) {
                controller.AttachKeyboardFilter();
            }
        }

        #endregion
    }

}

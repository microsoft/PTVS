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
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Watches for text views to be created for xaml code.  Then wires up to support analysis so that
    /// we can use the analysis for completion in .py code.
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType("xaml")]
    class XamlTextViewCreationListener : IVsTextViewCreationListener {
        internal readonly IVsEditorAdaptersFactoryService AdapterService;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public XamlTextViewCreationListener(IServiceProvider serviceProvider, IVsEditorAdaptersFactoryService adapterService) {
            _serviceProvider = serviceProvider;
            AdapterService = adapterService;
        }

        public void VsTextViewCreated(VisualStudio.TextManager.Interop.IVsTextView textViewAdapter) {
            // TODO: We should probably only track text views in Python projects or loose files.
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            
            if (textView != null) {
                var analyzer = textView.GetAnalyzer(_serviceProvider);
                if (analyzer != null) {
                    var monitorResult = analyzer.MonitorTextBuffer(textView, textView.TextBuffer);
                    textView.Closed += TextView_Closed;
                }
            }
        }

        private void TextView_Closed(object sender, EventArgs e) {
            var textView = (ITextView)sender;

            BufferParser bufferParser;
            if (textView.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out bufferParser)) {
                textView.GetAnalyzer(_serviceProvider).StopMonitoringTextBuffer(bufferParser, textView);
            }

            textView.Closed -= TextView_Closed;
        }
    }
}


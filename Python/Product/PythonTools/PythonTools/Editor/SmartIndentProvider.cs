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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ISmartIndentProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    public sealed class SmartIndentProvider : ISmartIndentProvider {
        private readonly PythonToolsService _pyService;

        [ImportingConstructor]
        internal SmartIndentProvider(PythonToolsService pyService) {
            _pyService = pyService;
        }

        private sealed class Indent : ISmartIndent {
            private readonly ITextView _textView;
            private readonly SmartIndentProvider _provider;

            public Indent(SmartIndentProvider provider, ITextView view) {
                _provider = provider;
                _textView = view;
            }

            public int? GetDesiredIndentation(ITextSnapshotLine line) {
                if (_provider._pyService.LangPrefs.IndentMode == vsIndentStyle.vsIndentStyleSmart) {
                    return AutoIndent.GetLineIndentation(line, _textView);
                } else {
                    return null;
                }
            }

            public void Dispose() {
            }
        }

        public ISmartIndent CreateSmartIndent(ITextView textView) {
            return new Indent(this, textView);
        }
    }
}

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

using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    internal class SmartIndent : ISmartIndent {
        private readonly PythonToolsService _pyService;
        private readonly ITextView _textView;

        public SmartIndent(PythonToolsService pyService, ITextView view) {
            _pyService = pyService ?? throw new ArgumentNullException(nameof(pyService));
            _textView = view ?? throw new ArgumentNullException(nameof(view));
        }

        public int? GetDesiredIndentation(ITextSnapshotLine line) {
            if (_pyService.LangPrefs.IndentMode == vsIndentStyle.vsIndentStyleSmart) {
                return AutoIndent.GetLineIndentation(PythonTextBufferInfo.ForBuffer(_pyService.Site, line.Snapshot.TextBuffer), line, _textView);
            } else {
                return null;
            }
        }

        public void Dispose() {
        }
    }
}

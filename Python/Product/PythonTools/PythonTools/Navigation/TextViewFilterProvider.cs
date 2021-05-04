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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Navigation {
    [Export(typeof(ITextViewCreationListener))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(TextViewFilterProvider))]
    class TextViewFilterProvider : ITextViewCreationListener {

        [Import]
        public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService = null;

        public void TextViewCreated(ITextView textView) {
            var vsTextView = EditorAdaptersFactoryService.GetViewAdapter(textView);
            if (vsTextView != null && !textView.Properties.ContainsProperty(typeof(TextViewFilter))) {
                var filter = new TextViewFilter(vsTextView);
                textView.Properties.AddProperty(typeof(TextViewFilter), filter);
            }
        }
    }
}

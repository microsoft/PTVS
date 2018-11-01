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

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
#if DEV16_OR_LATER
using Microsoft.WebTools.Languages.Editor.Controller;
using Microsoft.WebTools.Languages.Editor.Services;
#else
using Microsoft.Web.Editor.Controller;
using Microsoft.Web.Editor.Services;
#endif

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class TemplateMainController : ViewController {
        public TemplateMainController(ITextView textView, ITextBuffer textBuffer)
            : base(textView, textBuffer) {
            ServiceManager.AddService<TemplateMainController>(this, textView);
        }

        protected override void Dispose(bool disposing) {
            ServiceManager.RemoveService<TemplateMainController>(TextView);
            base.Dispose(disposing);
        }
    }
}

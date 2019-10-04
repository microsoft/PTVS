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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal class BraceHighlightTaggerProvider : IViewTaggerProvider {
        private readonly IServiceProvider _site;

        [ImportingConstructor]
        public BraceHighlightTaggerProvider([Import(typeof(SVsServiceProvider))] IServiceProvider site) {
            _site = site;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
            if (textView == null) {
                return null;
            }

            if (textView.TextBuffer != buffer) {
                return null;
            }

            return new BraceHighlightTagger(_site, textView, buffer) as ITagger<T>;
        }
    }
}

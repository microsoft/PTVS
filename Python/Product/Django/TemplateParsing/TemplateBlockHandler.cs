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
#if DJANGO_HTML_EDITOR
using System.Collections.Generic;
using Microsoft.WebTools.Languages.Html.Editor.ContainedLanguage.Generators;
using Microsoft.WebTools.Languages.Html.Editor.ContainedLanguage.Handlers;
using Microsoft.WebTools.Languages.Html.Editor.ContentType;
using Microsoft.WebTools.Languages.Html.Editor.Tree;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateBlockHandler : ArtifactBasedBlockHandler {
        private readonly List<TemplateArtifact> _trackedArtifacts = new List<TemplateArtifact>();

        public TemplateBlockHandler(HtmlEditorTree editorTree)
            : base(editorTree, ContentTypeManager.GetContentType(TemplateHtmlContentType.ContentTypeName)) {
        }

        protected override BufferGenerator CreateBufferGenerator() {
            return new TemplateBufferGenerator(EditorTree, LanguageBlocks);
        }

        protected override void OnUpdateCompleted(object sender, HtmlTreeUpdatedEventArgs e) {
            base.OnUpdateCompleted(sender, e);
            if (e.FullParse) {
                UpdateBuffer(force: true);
            }
        }
    }
}
#endif

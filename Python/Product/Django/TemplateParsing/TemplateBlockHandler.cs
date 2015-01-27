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

#if DEV12_OR_LATER

using System.Collections.Generic;

#if DEV14_OR_LATER
using Microsoft.Html.Editor.ContainedLanguage.Generators;
using Microsoft.Html.Editor.ContainedLanguage.Handlers;
using Microsoft.Html.Editor.ContentType;
using Microsoft.Html.Editor.Tree;
#else
using System;
using Microsoft.Html.Editor;
using Microsoft.Html.Editor.ContainedLanguage;
#endif

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
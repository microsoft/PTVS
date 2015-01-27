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

#if DEV14_OR_LATER
using Microsoft.Html.Core.Artifacts;
using Microsoft.Html.Editor.ContentType.Handlers;
using Microsoft.Html.Editor.Tree;
#else
using Microsoft.Html.Core;
using Microsoft.Html.Editor;
#endif

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateContentTypeHandler : HtmlServerCodeContentTypeHandler {
        public override void Init(HtmlEditorTree editorTree) {
            base.Init(editorTree);
            ContainedLanguageBlockHandler = new TemplateBlockHandler(editorTree);
        }

        public override ArtifactCollection CreateArtifactCollection() {
            return new TemplateArtifactCollection();
        }
    }
}

#endif
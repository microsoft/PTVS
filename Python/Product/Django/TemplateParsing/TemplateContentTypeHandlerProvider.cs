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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

#if DEV14_OR_LATER
using Microsoft.Html.Editor.ContentType.Def;
#else
using Microsoft.Html.Editor;
#endif

namespace Microsoft.PythonTools.Django.TemplateParsing {
    [Export(typeof(IContentTypeHandlerProvider))]
    [ContentType(TemplateHtmlContentType.ContentTypeName)]
    internal class TemplateContentTypeHandlerProvider : IContentTypeHandlerProvider {
        public IContentTypeHandler GetContentTypeHandler() {
            return new TemplateContentTypeHandler();
        }
    }
}

#endif
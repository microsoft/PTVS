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
using Microsoft.Web.Core.ContentTypes;
#else
using Microsoft.Web.Editor;
#endif

namespace Microsoft.PythonTools.Django {
    /// <summary>
    /// Content type for Django template files. This is the top-level type for the entire file, which is HTML on top
    /// with embedded Django tags, which is why it is derived from HTML. The HTML editor handles classification,
    /// completion etc for this type, except for the embedded Django tags.
    /// </summary>
    internal static class TemplateHtmlContentType {
        public const string FileExtension = ".djt";
        public const string ContentTypeName = "Django Templates";

        [Export, Name(ContentTypeName), BaseDefinition(HtmlContentTypeDefinition.HtmlContentType)]
        internal static ContentTypeDefinition ContentTypeDefinition;

        [Export, FileExtension(FileExtension), ContentType(ContentTypeName)]
        internal static FileExtensionToContentTypeDefinition FileExtensionToContentTypeDefinition;
    }

    /// <summary>
    /// Content type for the contents of Django tags embedded inside <see cref="TemplateHtmlContentType"/>.
    /// This does not include the boundaries of the tags (i.e. {% and %} etc), but only the text between
    /// them. This package handles classification and code completion for this content type.
    /// </summary>
    internal static class TemplateTagContentType {
        public const string ContentTypeName = "DjangoTemplateTag";

        [Export, Name(ContentTypeName), BaseDefinition("code")]
        internal static ContentTypeDefinition ContentTypeDefinition;
    }
}

#endif
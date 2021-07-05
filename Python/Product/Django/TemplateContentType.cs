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

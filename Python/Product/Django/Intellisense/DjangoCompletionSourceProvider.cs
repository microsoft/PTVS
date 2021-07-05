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

namespace Microsoft.PythonTools.Django.Intellisense {
    [Export(typeof(ICompletionSourceProvider)), ContentType(TemplateTagContentType.ContentTypeName), Order, Name("DjangoCompletionSourceProvider")]
    internal class DjangoCompletionSourceProvider : ICompletionSourceProvider {
        internal readonly IGlyphService _glyphService;
        internal readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public DjangoCompletionSourceProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider, IGlyphService glyphService) {
            _serviceProvider = serviceProvider;
            _glyphService = glyphService;
        }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
            var filename = textBuffer.GetFileName();
            if (filename != null) {
                var project = DjangoPackage.GetProject(_serviceProvider, filename);
                if (project != null) {
                    return new DjangoCompletionSource(_glyphService, project.Analyzer, _serviceProvider, textBuffer);
                }
            }
            return null;
        }
    }
}

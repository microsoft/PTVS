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
    [Export(typeof(IIntellisenseControllerProvider))]
    [Export(typeof(ICompletionControllerProvider))]
    [ContentType(TemplateTagContentType.ContentTypeName)]
    [Order]
    internal class TemplateCompletionControllerProvider : IIntellisenseControllerProvider, ICompletionControllerProvider {
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly PythonToolsService _pyService;
        private readonly IAsyncQuickInfoBroker _quickInfoBroker;

        [ImportingConstructor]
        public TemplateCompletionControllerProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider, ICompletionBroker completionBroker, IAsyncQuickInfoBroker quickInfoBroker, ISignatureHelpBroker signatureHelpBroker) {
            _completionBroker = completionBroker;
            _quickInfoBroker = quickInfoBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _pyService = (PythonToolsService)serviceProvider.GetService(typeof(PythonToolsService));
        }

        public CompletionController GetCompletionController(ITextView textView) {
            return ServiceManager.GetService<TemplateCompletionController>(textView);
        }

        public IIntellisenseController TryCreateIntellisenseController(ITextView view, IList<ITextBuffer> subjectBuffers) {
            var completionController = ServiceManager.GetService<TemplateCompletionController>(view);
            if (completionController == null) {
                completionController = new TemplateCompletionController(_pyService, view, subjectBuffers, _completionBroker, _quickInfoBroker, _signatureHelpBroker);
                ServiceManager.AddService<TemplateCompletionController>(completionController, view);
            }
            return completionController;
        }
    }
}

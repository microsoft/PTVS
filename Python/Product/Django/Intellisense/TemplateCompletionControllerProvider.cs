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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Web.Editor;

namespace Microsoft.PythonTools.Django.Intellisense {
    [Export(typeof(IIntellisenseControllerProvider)), ContentType(TemplateTagContentType.ContentTypeName), Order]
    internal class TemplateCompletionControllerProvider : IIntellisenseControllerProvider {
        private readonly ICompletionBroker _completionBroker;
        private readonly IQuickInfoBroker _quickInfoBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly PythonToolsService _pyService;

        [ImportingConstructor]
        public TemplateCompletionControllerProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, ICompletionBroker completionBroker, IQuickInfoBroker quickInfoBroker, ISignatureHelpBroker signatureHelpBroker) {
            _completionBroker = completionBroker;
            _quickInfoBroker = quickInfoBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _pyService = (PythonToolsService)serviceProvider.GetService(typeof(PythonToolsService));
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

#endif
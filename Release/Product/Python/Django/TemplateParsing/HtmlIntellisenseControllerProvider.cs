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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    [Export(typeof(IIntellisenseControllerProvider)), ContentType("HTML"), Order]
    class HtmlIntellisenseControllerProvider : IIntellisenseControllerProvider {
        internal readonly ICompletionBroker _CompletionBroker;
        internal readonly IVsEditorAdaptersFactoryService _adaptersFactory;

        [ImportingConstructor]
        public HtmlIntellisenseControllerProvider(ICompletionBroker broker, IVsEditorAdaptersFactoryService editorAdapter) {
            _CompletionBroker = broker;
            _adaptersFactory = editorAdapter;
        }

        #region IIntellisenseControllerProvider Members

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers) {
            HtmlIntellisenseController controller = null;
            if (!textView.Properties.TryGetProperty<HtmlIntellisenseController>(typeof(HtmlIntellisenseController), out controller)) {
                foreach (var buffer in subjectBuffers) {
                    if (buffer.Properties.ContainsProperty(typeof(DjangoEditorFactory))) { // it's one of our buffers
                        controller = new HtmlIntellisenseController(this, textView);
                        textView.Properties.AddProperty(typeof(HtmlIntellisenseController), controller);
                    }
                }
            }
            
            return controller;
        }

        #endregion

        internal static HtmlIntellisenseController GetOrCreateController(IComponentModel model, ITextView textView) {
            HtmlIntellisenseController controller;
            if (!textView.Properties.TryGetProperty<HtmlIntellisenseController>(typeof(HtmlIntellisenseController), out controller)) {
                var intellisenseControllerProvider = (
                   from export in model.DefaultExportProvider.GetExports<IIntellisenseControllerProvider, IContentTypeMetadata>()
                   from exportedContentType in export.Metadata.ContentTypes
                   where exportedContentType.Equals("HTML", StringComparison.OrdinalIgnoreCase) && export.Value.GetType() == typeof(HtmlIntellisenseControllerProvider)
                   select export.Value
                ).First();
                controller = new HtmlIntellisenseController((HtmlIntellisenseControllerProvider)intellisenseControllerProvider, textView);
                textView.Properties.AddProperty(typeof(HtmlIntellisenseController), controller);
            }
            return controller;
        }

    }
}

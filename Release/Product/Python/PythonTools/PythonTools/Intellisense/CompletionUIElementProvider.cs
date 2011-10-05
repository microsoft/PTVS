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
using System.Windows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Utilities;

namespace Microsoft.PythonTools.Intellisense {
    [Export(typeof(IUIElementProvider<CompletionSet, ICompletionSession>))]
    [Name("Python Completion UI Provider")]
    [Order()]
    [ContentType(PythonCoreConstants.ContentType)]
    internal class CompletionUIElementProvider : IUIElementProvider<CompletionSet, ICompletionSession> {
        [ImportMany]
        internal List<Lazy<IUIElementProvider<CompletionSet, ICompletionSession>, IOrderableContentTypeMetadata>> UnOrderedCompletionSetUIElementProviders { get; set; }

        public CompletionUIElementProvider() {
        }

        public UIElement GetUIElement(CompletionSet itemToRender, ICompletionSession context, UIElementType elementType) {
            var orderedProviders = Orderer.Order(UnOrderedCompletionSetUIElementProviders);
            foreach (var presenterProviderExport in orderedProviders) {

                foreach (var contentType in presenterProviderExport.Metadata.ContentTypes) {
                    if (PythonToolsPackage.Instance.ContentType.IsOfType(contentType)) {
                        if (presenterProviderExport.Value.GetType() == typeof(CompletionUIElementProvider)) {
                            // don't forward to ourselves...
                            continue;
                        }

                        var res = presenterProviderExport.Value.GetUIElement(itemToRender, context, elementType);
                        if (res != null) {
                            return new CompletionControl(res, context);
                        }
                    }
                }

            }

            return null;
        }
    }
}

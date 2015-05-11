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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Intellisense {
#if !DEV14_OR_LATER
    [Export(typeof(ISmartTagSourceProvider))]
    [Order(Before = Priority.Default)]
    [Name("Python Smart Tag Source Provider")]
    [ContentType(PythonCoreConstants.ContentType)]
    class SmartTagSourceProvider : ISmartTagSourceProvider {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public SmartTagSourceProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public ISmartTagSource TryCreateSmartTagSource(ITextBuffer textBuffer) {
            return new SmartTagSource(_serviceProvider, textBuffer);
        }
    }
#endif
}

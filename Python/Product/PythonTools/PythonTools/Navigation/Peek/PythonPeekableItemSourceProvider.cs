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

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Navigation.Peek {
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonPeekableItemSourceProvider))]
    [SupportsStandaloneFiles(true)]
    [SupportsPeekRelationship("IsDefinedBy")] // Value of PredefinedPeekRelationships.Definitions.Name
    internal sealed class PythonPeekableItemSourceProvider : IPeekableItemSourceProvider {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonPeekableItemSourceProvider(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            IPeekResultFactory peekResultFactory) {
            _serviceProvider = serviceProvider;
            _peekResultFactory = peekResultFactory;
        }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer) {
            if (textBuffer == null) {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new PythonPeekableItemSource(_serviceProvider, _peekResultFactory, textBuffer)
            );
        }
    }
}

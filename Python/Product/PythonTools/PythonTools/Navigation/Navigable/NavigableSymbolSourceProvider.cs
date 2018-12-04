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
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Navigation.Navigable {
    [Export(typeof(INavigableSymbolSourceProvider))]
    [Name(nameof(NavigableSymbolSourceProvider))]
    [ContentType(PythonCoreConstants.ContentType)]
    class NavigableSymbolSourceProvider : INavigableSymbolSourceProvider {
        private readonly IServiceProvider _serviceProvider;
        private readonly IClassifierAggregatorService _classifierFactory;
        private readonly ITextStructureNavigatorSelectorService _navigatorService;

        [ImportingConstructor]
        public NavigableSymbolSourceProvider(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            IClassifierAggregatorService classifierFactory,
            ITextStructureNavigatorSelectorService navigatorService
        ) {
            _serviceProvider = serviceProvider;
            _classifierFactory = classifierFactory;
            _navigatorService = navigatorService;
        }

        public INavigableSymbolSource TryCreateNavigableSymbolSource(ITextView textView, ITextBuffer buffer) {
            return buffer.Properties.GetOrCreateSingletonProperty<INavigableSymbolSource>(
                () => new NavigableSymbolSource(
                    _serviceProvider,
                    buffer,
                    _classifierFactory.GetClassifier(buffer),
                    _navigatorService.GetTextStructureNavigator(buffer))
            );
        }
    }
}

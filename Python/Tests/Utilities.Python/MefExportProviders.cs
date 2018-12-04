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
using Microsoft.VisualStudio.Composition;
using TestUtilities.Mocks;

namespace TestUtilities.Python {
    public static class MefExportProviders {
        private static readonly Lazy<IExportProviderFactory> _editorExportProviderFactoryLazy = new Lazy<IExportProviderFactory>(CreateEditorExportProviderFactory);

        private static IExportProviderFactory CreateEditorExportProviderFactory() {
            AssemblyLoader.EnsureLoaded(
                "Microsoft.VisualStudio.VsInteractiveWindow",
                "Microsoft.VisualStudio.Editor.Implementation",
                "Microsoft.VisualStudio.Platform.VSEditor",
                "Microsoft.PythonTools.VSInterpreters");

            var catalog = MefCatalogFactory.CreateAssembliesCatalog(
                    "Microsoft.VisualStudio.CoreUtility",
                    "Microsoft.VisualStudio.Text.Data",
                    "Microsoft.VisualStudio.Text.Logic",
                    "Microsoft.VisualStudio.Text.UI",
                    "Microsoft.VisualStudio.Text.UI.Wpf",
                    "Microsoft.VisualStudio.InteractiveWindow",
                    "Microsoft.VisualStudio.VsInteractiveWindow",
                    "Microsoft.VisualStudio.Editor",
                    "Microsoft.VisualStudio.Language.Intellisense",
                    "Microsoft.VisualStudio.Platform.VSEditor",
                    "Microsoft.PythonTools",
                    "Microsoft.PythonTools.VSInterpreters")
                .WithCompositionService()
                .WithServiceProvider()
                .AddJoinableTaskContext()
                .AddType<MockTextUndoHistoryRegistry>()
                .AddTypesFromAssembly("Microsoft.VisualStudio.Editor.Implementation",
                    "Microsoft.VisualStudio.Editor.Implementation.LoggingServiceInternal",
                    "Microsoft.VisualStudio.Editor.Implementation.PeekResultFactory",
                    "Microsoft.VisualStudio.Editor.Implementation.TipManager",
                    "Microsoft.VisualStudio.Editor.Implementation.VisualStudioWaitIndicator",
                    "Microsoft.VisualStudio.Editor.Implementation.VsEditorAdaptersFactoryService");

            var configuration = CompositionConfiguration.Create(catalog);
            var runtimeConfiguration = RuntimeComposition.CreateRuntimeComposition(configuration);
            return runtimeConfiguration.CreateExportProviderFactory();
        }

        public static ExportProvider CreateEditorExportProvider()
            => _editorExportProviderFactoryLazy.Value.CreateExportProvider();
    }
}
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
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.LanguageServerClient {
    abstract class PythonFilePathToContentTypeProvider : IFilePathToContentTypeProvider {
        [Import(typeof(SVsServiceProvider))]
        public IServiceProvider Site;

        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        public IVsEditorAdaptersFactoryService EditorAdapterFactoryService;

        [Import]
        public IVsFolderWorkspaceService WorkspaceService;

        [Import]
        public IInterpreterOptionsService OptionsService;

        [Import]
        public IInterpreterRegistryService RegistryService;

        [Import]
        public ILanguageClientBroker Broker;

        [Import]
        public IPythonWorkspaceContextProvider PythonWorkspaceContextProvider;

        public bool TryGetContentTypeForFilePath(string filePath, out IContentType contentType) {
            contentType = null;

            // Force the package to load, since this is a MEF component, there is no guarantee it has been loaded.
            Site.GetPythonToolsService();

            string contentTypeName;
            var project = Site.GetProjectContainingFile(filePath);
            if (project != null) {
                contentTypeName = GetContentTypeNameForProject(project);
            } else if (WorkspaceService.CurrentWorkspace != null) {
                contentTypeName = GetContentTypeNameForWorkspace(WorkspaceService.CurrentWorkspace);
            } else {
                contentTypeName = GetContentTypeNameForLoosePythonFile();
            }

            if (contentTypeName != null) {
                contentType = GetOrCreateContentType(ContentTypeRegistryService, contentTypeName);
                
                Site.GetUIThread().InvokeTaskSync(() => PythonLanguageClient.EnsureLanguageClientAsync(
                    Site,
                    WorkspaceService,
                    PythonWorkspaceContextProvider,
                    OptionsService,
                    RegistryService,
                    Broker,
                    contentTypeName,
                    project,
                    null
                ), CancellationToken.None);
            }

            return contentType != null;
        }

        public static string GetContentTypeNameForProject(PythonProjectNode project) {
            // To avoid having to track project renames, use the project guid, not the project name
            return "PythonProject:{0}".FormatInvariant(project.ProjectIDGuid);
        }

        public static string GetContentTypeNameForProject(IPythonProject project) {
            // To avoid having to track project renames, use the project guid, not the project name
            if (project is PythonProjectNode pythonProject) {
                return GetContentTypeNameForProject(pythonProject);
            }

            return null;
        }

        public static string GetContentTypeNameForWorkspace(IWorkspace workspace) {
            return "PythonWorkspace:{0}".FormatInvariant(workspace.GetName());
        }

        public static string GetContentTypeNameForREPL(int windowId) {
            return "PythonInteractive:{0}".FormatInvariant(windowId);
        }

        public static string GetContentTypeNameForLoosePythonFile() {
            return "PythonFile";
        }

        public static IContentType GetOrCreateContentType(
            IContentTypeRegistryService contentTypeRegistryService,
            string contentTypeName
        ) {
            return GetOrCreateContentType(
                contentTypeRegistryService,
                contentTypeName,
                new[] { PythonCoreConstants.ContentType }
            );
        }

        public static IContentType GetOrCreateContentType(
            IContentTypeRegistryService contentTypeRegistryService,
            string contentTypeName,
            string[] baseContentTypeNames
        ) {
            var contentType = contentTypeRegistryService.GetContentType(contentTypeName);
            if (contentType == null) {
                contentType = contentTypeRegistryService.AddContentType(
                    contentTypeName,
                    baseContentTypeNames
                );
            }

            return contentType;
        }
    }

    [Export(typeof(IFilePathToContentTypeProvider))]
    [Name(nameof(PyFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.FileExtension)]
    class PyFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }

    [Export(typeof(IFilePathToContentTypeProvider))]
    [Name(nameof(PywFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.WindowsFileExtension)]
    class PywFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }

    [Export(typeof(IFilePathToContentTypeProvider))]
    [Name(nameof(PyiFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.StubFileExtension)]
    class PyiFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }
}

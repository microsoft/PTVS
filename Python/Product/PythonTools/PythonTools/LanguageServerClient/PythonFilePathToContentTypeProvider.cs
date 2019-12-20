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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal abstract class PythonFilePathToContentTypeProvider : IFilePathToContentTypeProvider {
        [Import(typeof(SVsServiceProvider))]
        public IServiceProvider Site;

        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        public IPythonWorkspaceContextProvider PythonWorkspaceContextProvider;

        [Import]
        public IInterpreterOptionsService OptionsService;

        [Import]
        public JoinableTaskContext JoinableTaskContext;

        public bool TryGetContentTypeForFilePath(string filePath, out IContentType contentType) {
            contentType = null;

            ThreadHelper.ThrowIfNotOnUIThread();

            // In LiveShare client scenario, we'll be called with files in temp
            // folder created by LiveShare, do not create a content type or
            // start a language server for those! Default content type will be
            // Python, and LiveShare will pass that to the host who will dispatch
            // to its correct language server.
            if (Site.IsInLiveShareClientSession()) {
                return false;
            }

            // Force the package to load, since this is a MEF component,
            // there is no guarantee it has been loaded.
            Site.GetPythonToolsService();

            var project = Site.GetProjectContainingFile(filePath);
            if (project != null) {
                contentType = ProcessProject(project);
            } else if (PythonWorkspaceContextProvider.Workspace != null) {
                contentType = ProcessWorkspace(PythonWorkspaceContextProvider.Workspace);
            } else {
                contentType = ProcessGlobal();
            }

            return contentType != null;
        }

        private IContentType ProcessProject(PythonProjectNode project) {
            var contentTypeName = GetContentTypeNameForProject(project);
            var context = new PythonLanguageClientContextProject(project, contentTypeName);
            return EnsureContentTypeAndLanguageClient(contentTypeName, context);
        }

        private IContentType ProcessWorkspace(IPythonWorkspaceContext workspace) {
            var contentTypeName = GetContentTypeNameForWorkspace(workspace);
            var context = new PythonLanguageClientContextWorkspace(workspace, contentTypeName);
            return EnsureContentTypeAndLanguageClient(contentTypeName, context);
        }

        private IContentType ProcessGlobal() {
            var contentTypeName = GetContentTypeNameForGlobalPythonFile();
            var context = new PythonLanguageClientContextGlobal(OptionsService, contentTypeName);
            return EnsureContentTypeAndLanguageClient(contentTypeName, context);
        }

        private IContentType EnsureContentTypeAndLanguageClient(string contentTypeName, IPythonLanguageClientContext context) {
            var contentType = GetOrCreateContentType(ContentTypeRegistryService, contentTypeName);

            Site.GetUIThread().InvokeTaskSync(() => PythonLanguageClient.EnsureLanguageClientAsync(
                Site,
                JoinableTaskContext,
                context
            ), CancellationToken.None);

            return contentType;
        }

        public static string GetContentTypeNameForProject(PythonProjectNode project) {
            // To avoid having to track project renames, use the project guid, not the project name
            return "PythonProject:{0}".FormatInvariant(project.ProjectIDGuid);
        }

        public static string GetContentTypeNameForWorkspace(IPythonWorkspaceContext workspace) {
            return "PythonWorkspace:{0}".FormatInvariant(workspace.WorkspaceName);
        }

        public static string GetContentTypeNameForREPL(int windowId) {
            return "PythonInteractive:{0}".FormatInvariant(windowId);
        }

        public static string GetContentTypeNameForGlobalPythonFile() {
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
    [Export(typeof(PyFilePathToContentTypeProvider))]
    [Name(nameof(PyFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.FileExtension)]
    internal class PyFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }

    [Export(typeof(IFilePathToContentTypeProvider))]
    [Name(nameof(PywFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.WindowsFileExtension)]
    internal class PywFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }

    [Export(typeof(IFilePathToContentTypeProvider))]
    [Name(nameof(PyiFilePathToContentTypeProvider))]
    [FileExtension(PythonConstants.StubFileExtension)]
    internal class PyiFilePathToContentTypeProvider : PythonFilePathToContentTypeProvider {
    }
}

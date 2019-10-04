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
using System.Collections.Generic;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.LanguageServerClient {
    // We're using PythonFilePathToContentTypeProvider now instead of this, keeping
    // this around for now just in case there are issues and we need to go back.
    class PythonLanguageClientDocumentTracker : IVsRunningDocTableEvents, IDisposable {
        private IServiceProvider _site;
        private RunningDocumentTable _runDocTable;
        private uint _runDocTableEventsCookie;
        private IVsEditorAdaptersFactoryService _editorAdapterFactoryService;
        private IVsFolderWorkspaceService _workspaceService;
        private IInterpreterOptionsService _optionsService;
        private IInterpreterRegistryService _registryService;
        private ILanguageClientBroker _broker;

        public PythonLanguageClientDocumentTracker() {
        }

        public void Dispose() {
            if (_site != null) {
                _runDocTable.Unadvise(_runDocTableEventsCookie);
            }
        }

        public void Initialize(IServiceProvider site) {
            _site = site;

            _runDocTable = new RunningDocumentTable(site);
            _runDocTableEventsCookie = _runDocTable.Advise(this);

            var componentModel = (IComponentModel)site.GetService(typeof(SComponentModel));
            _editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            _workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            _optionsService = componentModel.GetService<IInterpreterOptionsService>();
            _registryService = componentModel.GetService<IInterpreterRegistryService>();
            _broker = componentModel.GetService<ILanguageClientBroker>();

            //var nameToProjectMap = HandleLoadedDocuments();
            //foreach (var kv in nameToProjectMap) {
            //    EnsureLanguageClient(kv.Key, kv.Value);
            //}
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie) {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) {
            if (fFirstShow != 0) {
                var info = _runDocTable.GetDocumentInfo(docCookie);
                var (name, project) = HandleDocument(info);
                if (!string.IsNullOrEmpty(name)) {
                    EnsureLanguageClient(name, project);
                }
            }

            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
            return VSConstants.S_OK;
        }

        private IDictionary<string, PythonProjectNode> HandleLoadedDocuments() {
            var nameToProjectMap = new Dictionary<string, PythonProjectNode>();

            foreach (var info in _runDocTable) {
                var (name, project) = HandleDocument(info);
                if (!string.IsNullOrEmpty(name)) {
                    nameToProjectMap[name] = project;
                }
            }

            return nameToProjectMap;
        }

        private (string, PythonProjectNode) HandleDocument(RunningDocumentInfo info) {
            string name = null;
            PythonProjectNode project = null;

            if (info.IsDocumentInitialized && info.DocData != null && info.Hierarchy != null) {
                // old code that was working
                //var vsTextBuffer = info.DocData as IVsTextBuffer;
                //var textBuffer = info.DocData as ITextBuffer;
                //if (textBuffer == null && vsTextBuffer != null) {
                //    textBuffer = _editorAdapterFactoryService.GetDocumentBuffer(vsTextBuffer);
                //}

                if (info.DocData is IVsUserData vsUserData) {
                    vsUserData.GetData(VisualStudio.Editor.DefGuidList.guidDocumentTextSnapshot, out object snapshot);
                    if (snapshot != null) {
                        var textBuffer = (snapshot as ITextSnapshot)?.TextBuffer;
                        if (textBuffer != null) {
                            var contentType = textBuffer.ContentType;
                            if (contentType.IsOfType(PythonCoreConstants.ContentType)) {
                                if (!textBuffer.Properties.TryGetProperty(LanguageClientConstants.ClientNamePropertyKey, out name)) {
                                    name = info.Hierarchy.GetNameProperty();
                                    project = info.Hierarchy.GetPythonProject();
                                    textBuffer.Properties.AddProperty(LanguageClientConstants.ClientNamePropertyKey, name);
                                }
                            }
                        }
                    }
                }
            }

            return (name, project);
        }

        private void EnsureLanguageClient(string name, PythonProjectNode project) {
            //_site.GetUIThread().InvokeTaskSync(() => PythonLanguageClient.EnsureLanguageClientAsync(
            //    _site,
            //    _workspaceService,
            //    _optionsService,
            //    _registryService,
            //    _broker,
            //    name,
            //    project,
            //    null
            //), CancellationToken.None);
        }
    }
}

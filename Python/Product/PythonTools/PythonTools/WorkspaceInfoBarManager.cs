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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools {
    class WorkspaceInfoBarManager : IVsRunningDocTableEvents, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _pythonWorkspaceService;
        private readonly IVsRunningDocumentTable _docTable;

        private PackageInstallInfoBar _packageInstallInfoBar;
        private CondaEnvCreateInfoBar _condaEnvCreateInfoBar;
        private VirtualEnvCreateInfoBar _virtualEnvCreateInfoBar;
        private TestFrameworkWorkspaceInfoBar _testFrameworkInfoBar;
        private PythonNotSupportedInfoBar _pythonVersionNotSupportedInfoBar;
        private bool _infoBarCheckTriggered;

        public WorkspaceInfoBarManager(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _pythonWorkspaceService = serviceProvider.GetComponentModel().GetService<IPythonWorkspaceContextProvider>();
            _pythonWorkspaceService.WorkspaceInitialized += OnWorkspaceInitialized;
            _docTable = _serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
        }

        public void Dispose() {
            _pythonWorkspaceService.WorkspaceInitialized -= OnWorkspaceInitialized;
        }

        private void OnWorkspaceInitialized(object sender, PythonWorkspaceContextEventArgs e) {
            var workspace = e.Workspace;
            _infoBarCheckTriggered = false;

            _packageInstallInfoBar = new PackageInstallWorkspaceInfoBar(_serviceProvider, workspace);
            _condaEnvCreateInfoBar = new CondaEnvCreateWorkspaceInfoBar(_serviceProvider, workspace);
            _virtualEnvCreateInfoBar = new VirtualEnvCreateWorkspaceInfoBar(_serviceProvider, workspace);
            _testFrameworkInfoBar = new TestFrameworkWorkspaceInfoBar(_serviceProvider, workspace);
            _pythonVersionNotSupportedInfoBar = new PythonNotSupportedInfoBar(_serviceProvider, InfoBarContexts.Workspace, () => workspace.CurrentFactory);

            workspace.AddActionOnClose(_packageInstallInfoBar, (obj => ((PythonInfoBar)obj).Dispose()));
            workspace.AddActionOnClose(_condaEnvCreateInfoBar, (obj => ((PythonInfoBar)obj).Dispose()));
            workspace.AddActionOnClose(_virtualEnvCreateInfoBar, (obj => ((PythonInfoBar)obj).Dispose()));
            workspace.AddActionOnClose(_testFrameworkInfoBar, (obj => ((PythonInfoBar)obj).Dispose()));
            workspace.AddActionOnClose(
                _pythonVersionNotSupportedInfoBar,
                 obj => { ((PythonInfoBar)obj).Dispose(); workspace.ActiveInterpreterChanged -= TriggerPythonNotSupportedInforBar; }
            );

            workspace.ActiveInterpreterChanged += TriggerPythonNotSupportedInforBar;

            // When we see a Python file opened in the workspace, we trigger info bar checks.
            // Python files may have already been opened by the time this runs, so we'll check
            // the already loaded files first. If there are no Python file that trigger info bar
            // checks, then we'll register to be notified when files are opened.
            if (ErrorHandler.Succeeded(_docTable.GetRunningDocumentsEnum(out var pEnumRdt))) {
                if (ErrorHandler.Succeeded(pEnumRdt.Reset())) {
                    uint[] cookie = new uint[1];
                    while (VSConstants.S_OK == pEnumRdt.Next(1, cookie, out _)) {
                        var docFilePath = GetDocumentFilePath(cookie[0]);
                        if (IsWorkspacePythonFile(docFilePath)) {
                            TriggerInfoBar();
                            break;
                        }
                    }
                }
            }

            if (!_infoBarCheckTriggered) {
                if (ErrorHandler.Succeeded(_docTable.AdviseRunningDocTableEvents(this, out uint eventCookie))) {
                    workspace.AddActionOnClose(_docTable, obj => _docTable.UnadviseRunningDocTableEvents(eventCookie));
                }
            }
        }

        private string GetDocumentFilePath(uint docCookie) {
            var hr = _docTable.GetDocumentInfo(
                docCookie,
                out _,
                out _,
                out _,
                out string docFilePath,
                out _,
                out _,
                out IntPtr ppunkDocData
            );

            if (ErrorHandler.Succeeded(hr)) {
                if (ppunkDocData != IntPtr.Zero) {
                    Marshal.Release(ppunkDocData);
                }

                return docFilePath;
            }

            return null;
        }

        private bool IsWorkspacePythonFile(string filePath) {
            return !string.IsNullOrEmpty(filePath) &&
                   PathUtils.IsValidPath(filePath) &&
                   File.Exists(filePath) &&
                   ModulePath.IsPythonSourceFile(filePath) &&
                   _pythonWorkspaceService.Workspace != null &&
                   PathUtils.IsSubpathOf(_pythonWorkspaceService.Workspace.Location, filePath);
        }

        private void TriggerInfoBar() {
            _infoBarCheckTriggered = true;
            TriggerInfoBarsAsync().HandleAllExceptions(_serviceProvider, typeof(WorkspaceInfoBarManager)).DoNotWait();
        }

        private async Task TriggerInfoBarsAsync() {
            await Task.WhenAll(
                _condaEnvCreateInfoBar.CheckAsync(),
                _virtualEnvCreateInfoBar.CheckAsync(),
                _packageInstallInfoBar.CheckAsync(),
                _testFrameworkInfoBar.CheckAsync(),
                _pythonVersionNotSupportedInfoBar.CheckAsync()
            );
        }

        private void TriggerPythonNotSupportedInforBar(object sender, EventArgs e) {
            TriggerPythonNotSupportedInforBarAsync().HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private Task TriggerPythonNotSupportedInforBarAsync() {
            return _pythonVersionNotSupportedInfoBar.CheckAsync();
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
            if (!_infoBarCheckTriggered && fFirstShow != 0) {
                if (IsWorkspacePythonFile(GetDocumentFilePath(docCookie))) {
                    TriggerInfoBar();
                }
            }

            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
            return VSConstants.S_OK;
        }
    }
}

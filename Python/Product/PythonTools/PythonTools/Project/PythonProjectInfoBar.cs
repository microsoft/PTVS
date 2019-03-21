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
using System.Diagnostics;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal abstract class PythonProjectInfoBar : IVsInfoBarUIEvents, IDisposable {
        private readonly IVsShell _shell;
        private readonly IVsInfoBarUIFactory _infoBarFactory;
        private readonly IdleManager _idleManager;
        private uint _adviseCookie;
        private IVsInfoBarUIElement _infoBar;
        private InfoBarModel _infoBarModel;

        protected PythonProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode) {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
            Logger = (IPythonToolsLogger)site.GetService(typeof(IPythonToolsLogger));
            _shell = (IVsShell)site.GetService(typeof(SVsShell));
            _infoBarFactory = (IVsInfoBarUIFactory)site.GetService(typeof(SVsInfoBarUIFactory));
            _idleManager = new IdleManager(site);
        }

        protected PythonProjectInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace) {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Logger = (IPythonToolsLogger)site.GetService(typeof(IPythonToolsLogger));
            _shell = (IVsShell)site.GetService(typeof(SVsShell));
            _infoBarFactory = (IVsInfoBarUIFactory)site.GetService(typeof(SVsInfoBarUIFactory));
            _idleManager = new IdleManager(site);
        }

        protected IServiceProvider Site { get; }

        protected PythonProjectNode Project { get; }

        protected IPythonWorkspaceContext Workspace { get; }

        protected IPythonToolsLogger Logger { get; }

        protected bool IsCreated => _infoBar != null;

        public abstract Task CheckAsync();

        protected void Create(InfoBarModel model) {
            _infoBarModel = model;
            FinishCreate();
        }

        protected void Close() {
            _infoBar.Close();
        }

        protected void FinishCreate() {
            if (_infoBar != null) {
                Debug.Fail("Should not try to create info bar more than once.");
                return;
            }

            if (ErrorHandler.Failed(_shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out object infoBarHostObj)) || infoBarHostObj == null) {
                // Main window is not ready yet, finish creating it later
                _idleManager.OnIdle += OnIdle;
                return;
            }

            var infoBarHost = (IVsInfoBarHost)infoBarHostObj;

            _infoBar = _infoBarFactory.CreateInfoBar(_infoBarModel);
            infoBarHost.AddInfoBar(_infoBar);

            _infoBar.Advise(this, out uint cookie);
            _adviseCookie = cookie;
        }

        protected bool IsSuppressed(string propertyName) {
            if (Project != null) {
                var suppressProp = Project.GetProjectProperty(propertyName);
                return suppressProp.IsTrue();
            } else if (Workspace != null) {
                var suppressProp = Workspace.GetBoolProperty(propertyName);
                return suppressProp.HasValue ? suppressProp.Value : false;
            }

            return false;
        }

        protected async Task SuppressAsync(string propertyName) {
            if (Project != null) {
                Project.SetProjectProperty(propertyName, true.ToString());
            } else if (Workspace != null) {
                await Workspace.SetPropertyAsync(propertyName, true);
            }
        }

        private void OnIdle(object sender, ComponentManagerEventArgs e) {
            _idleManager.OnIdle -= OnIdle;
            FinishCreate();
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
            ((Action)actionItem.ActionContext)();
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement) {
            infoBarUIElement.Unadvise(_adviseCookie);
            _infoBar = null;
        }

        public void Dispose() {
            _infoBar?.Close();
            _idleManager.OnIdle -= OnIdle;
            _idleManager.Dispose();
        }
    }
}

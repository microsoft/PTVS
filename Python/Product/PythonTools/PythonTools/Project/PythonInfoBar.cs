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
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal abstract class PythonInfoBar : IVsInfoBarUIEvents, IDisposable {
        private readonly IVsShell _shell;
        private readonly IVsInfoBarUIFactory _infoBarFactory;
        private readonly IdleManager _idleManager;
        private uint _adviseCookie;
        private IVsInfoBarUIElement _infoBar;
        private InfoBarModel _infoBarModel;

        protected PythonInfoBar(IServiceProvider site) {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Logger = (IPythonToolsLogger)site.GetService(typeof(IPythonToolsLogger));
            _shell = (IVsShell)site.GetService(typeof(SVsShell));
            _infoBarFactory = (IVsInfoBarUIFactory)site.GetService(typeof(SVsInfoBarUIFactory));
            _idleManager = new IdleManager(site);
        }

        protected IServiceProvider Site { get; }

        protected IPythonToolsLogger Logger { get; }

        protected bool IsCreated => _infoBar != null;

        public abstract Task CheckAsync();

        protected void Create(InfoBarModel model) {
            _infoBarModel = model;
            FinishCreate();
        }

        protected void Close() {
            _infoBar?.Close();
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

        private void OnIdle(object sender, ComponentManagerEventArgs e) {
            _idleManager.OnIdle -= OnIdle;
            FinishCreate();
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
            if (actionItem.ActionContext != null) {
                ((Action)actionItem.ActionContext)();
            }
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

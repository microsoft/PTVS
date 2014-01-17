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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    class OutputWindowRedirector : Redirector {
        private static readonly Guid OutputWindowGuid = new Guid("{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}");
        static OutputWindowRedirector _generalPane;

        public static OutputWindowRedirector Get(IServiceProvider provider, Guid id, string title) {
            IVsOutputWindow outputWindow = provider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            IVsOutputWindowPane pane;
            if (ErrorHandler.Failed(outputWindow.GetPane(id, out pane)) || pane == null) {
                ErrorHandler.ThrowOnFailure(outputWindow.CreatePane(id, title, 1, 0));
            }
            return new OutputWindowRedirector(provider, id);
        }

        public static OutputWindowRedirector GetGeneral(IServiceProvider provider) {
            if (_generalPane == null) {
                _generalPane = Get(provider, VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "General");
            }
            return _generalPane;
        }

        readonly IVsWindowFrame _window;
        readonly IVsOutputWindowPane _pane;

        public IVsOutputWindowPane Pane { get { return _pane; } }

        public OutputWindowRedirector(IServiceProvider provider, Guid paneGuid) {
            var shell = provider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (shell != null) {
                // Ignore errors - we just won't support opening the window if
                // we don't find it.
                var windowGuid = OutputWindowGuid;
                shell.FindToolWindow(0, ref windowGuid, out _window);
            }
            IVsOutputWindow outputWindow = provider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            ErrorHandler.ThrowOnFailure(outputWindow.GetPane(paneGuid, out _pane));
        }

        public OutputWindowRedirector(IVsWindowFrame window, IVsOutputWindowPane pane) {
            _window = window;
            if (pane == null) {
                throw new ArgumentNullException("pane");
            }
            _pane = pane;
        }

        public override void Show() {
            if (UIThread.Instance.IsUIThread) {
                ErrorHandler.ThrowOnFailure(_pane.Activate());
            } else {
                UIThread.Instance.Run(Show);
            }
        }

        public override void ShowAndActivate() {
            if (UIThread.Instance.IsUIThread) {
                ErrorHandler.ThrowOnFailure(_pane.Activate());
                if (_window != null) {
                    ErrorHandler.ThrowOnFailure(_window.ShowNoActivate());
                }
            } else {
                UIThread.Instance.Run(ShowAndActivate);
            }
        }

        public override void WriteLine(string line) {
            _pane.OutputStringThreadSafe(line + Environment.NewLine);
        }

        public override void WriteErrorLine(string line) {
            _pane.OutputStringThreadSafe(line + Environment.NewLine);
        }
    }
}

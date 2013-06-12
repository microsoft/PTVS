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
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools {
    class OutputWindowRedirector : Redirector {
        static OutputWindowRedirector _generalPane;

        public static OutputWindowRedirector GetGeneral(IServiceProvider provider) {
            if (_generalPane == null) {
                IVsOutputWindow outputWindow = provider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                // Create the General pane if it doesn't exist
                outputWindow.CreatePane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "General", 1, 0);
                _generalPane = new OutputWindowRedirector(provider, VSConstants.OutputWindowPaneGuid.GeneralPane_guid);
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
                var windowGuid = VSConstants.StandardToolWindows.Output;
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
                if (_window != null) {
                    // TODO: Make showing window optional
                    ErrorHandler.ThrowOnFailure(_window.ShowNoActivate());
                }
            } else {
                UIThread.Instance.Run(Show);
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

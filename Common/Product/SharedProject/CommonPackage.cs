// Visual Studio Shared Project
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

using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools
{
    public abstract class CommonPackage : AsyncPackage, IOleComponent
    {
        private uint _componentID;
        private LibraryManager _libraryManager;
        private IOleComponentManager _compMgr;
        private UIThreadBase _uiThread;
        private static readonly object _commandsLock = new object();
        private static readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        #region Language-specific abstracts

        public abstract Type GetLibraryManagerType();
        internal abstract LibraryManager CreateLibraryManager();
        public abstract bool IsRecognizedFile(string filename);

        // TODO:
        // public abstract bool TryGetStartupFileAndDirectory(out string filename, out string dir);

        #endregion

        internal CommonPackage()
        {
#if DEBUG
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                if (e.IsTerminating) {
                    var ex = e.ExceptionObject as Exception;
                    if (ex is SEHException) {
                        return;
                    }

                    if (ex != null) {
                        Debug.Fail(
                            string.Format("An unhandled exception is about to terminate the process:\n\n{0}", ex.Message),
                            ex.ToString()
                        );
                    } else {
                        Debug.Fail(string.Format(
                            "An unhandled exception is about to terminate the process:\n\n{0}",
                            e.ExceptionObject
                        ));
                    }
                }
            };
#endif
        }


        internal static Dictionary<Command, MenuCommand> Commands => _commands;
        internal static object CommandsLock => _commandsLock;

        protected override void Dispose(bool disposing)
        {
            _uiThread.MustBeCalledFromUIThreadOrThrow();
            try
            {
                if (_componentID != 0)
                {
                    _compMgr.FRevokeComponent(_componentID);
                    _componentID = 0;
                }

                if (_libraryManager != null)
                {
                    _libraryManager.Dispose();
                    _libraryManager = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private object CreateLibraryManager(IServiceContainer container, Type serviceType)
        {
            if (GetLibraryManagerType() != serviceType)
            {
                return null;
            }

            return _libraryManager = CreateLibraryManager();
        }

        internal void RegisterCommands(Guid cmdSet, params Command[] commands)
        {
            _uiThread.MustBeCalledFromUIThreadOrThrow();
            if (GetService(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
            {
                lock (_commandsLock)
                {
                    foreach (var command in commands)
                    {
                        var beforeQueryStatus = command.BeforeQueryStatus;
                        CommandID toolwndCommandID = new CommandID(cmdSet, command.CommandId);
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);
                        if (beforeQueryStatus != null)
                        {
                            menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        }
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        internal void RegisterCommands(params MenuCommand[] commands)
        {
            _uiThread.MustBeCalledFromUIThreadOrThrow();
            if (GetService(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
            {
                foreach (var command in commands)
                {
                    mcs.AddCommand(command);
                }
            }
        }

        /// <summary>
        /// Gets the current IWpfTextView that is the active document.
        /// </summary>
        /// <returns></returns>
        public static IWpfTextView GetActiveTextView(System.IServiceProvider serviceProvider)
        {
            var monitorSelection = (IVsMonitorSelection)serviceProvider.GetService(typeof(SVsShellMonitorSelection));
            if (monitorSelection == null)
            {
                return null;
            }

            if (ErrorHandler.Failed(monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out var curDocument)))
            {
                // TODO: Report error
                return null;
            }

            if (!(curDocument is IVsWindowFrame frame))
            {
                // TODO: Report error
                return null;
            }

            if (ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var docView)))
            {
                // TODO: Report error
                return null;
            }
#if DEV11_OR_LATER
            if (docView is IVsDifferenceCodeWindow diffWindow && diffWindow.DifferenceViewer != null) {
                switch (diffWindow.DifferenceViewer.ActiveViewType) {
                    case VisualStudio.Text.Differencing.DifferenceViewType.InlineView:
                        return diffWindow.DifferenceViewer.InlineView;
                    case VisualStudio.Text.Differencing.DifferenceViewType.LeftView:
                        return diffWindow.DifferenceViewer.LeftView;
                    case VisualStudio.Text.Differencing.DifferenceViewType.RightView:
                        return diffWindow.DifferenceViewer.RightView;
                    default:
                        return null;
                }
            }
#endif
            if (docView is IVsCodeWindow window)
            {
                if (ErrorHandler.Failed(window.GetPrimaryView(out var textView)))
                {
                    // TODO: Report error
                    return null;
                }

                var model = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
                var adapterFactory = model.GetService<IVsEditorAdaptersFactoryService>();
                var wpfTextView = adapterFactory.GetWpfTextView(textView);
                return wpfTextView;
            }
            return null;
        }

        internal static CommonProjectNode GetStartupProject(System.IServiceProvider serviceProvider)
        {
            var buildMgr = (IVsSolutionBuildManager)serviceProvider.GetService(typeof(IVsSolutionBuildManager));
            if (buildMgr != null && ErrorHandler.Succeeded(buildMgr.get_StartupProject(out var hierarchy)) && hierarchy != null)
            {
                return hierarchy.GetProject()?.GetCommonProject();
            }
            return null;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _uiThread = (UIThreadBase)GetService(typeof(UIThreadBase));
            if (_uiThread == null)
            {
                _uiThread = new UIThread(JoinableTaskFactory);
                AddService<UIThreadBase>(_uiThread, true);
            }

            AddService(GetLibraryManagerType(), CreateLibraryManager, true);

            var crinfo = new OLECRINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO)),
                grfcrf = (uint)_OLECRF.olecrfNeedIdleTime,
                grfcadvf = (uint)_OLECADVF.olecadvfModal | (uint)_OLECADVF.olecadvfRedrawOff | (uint)_OLECADVF.olecadvfWarningsOff,
                uIdleTimeInterval = 0
            };

            _compMgr = (IOleComponentManager)GetService(typeof(SOleComponentManager));
            ErrorHandler.ThrowOnFailure(_compMgr.FRegisterComponent(this, new[] { crinfo }, out _componentID));

            await base.InitializeAsync(cancellationToken, progress);
        }

        protected override object GetService(Type serviceType)
            => serviceType == typeof(UIThreadBase) ? _uiThread : base.GetService(serviceType);

        protected void AddService<T>(object service, bool promote)
            => ((IServiceContainer)this).AddService(typeof(T), service, promote);

        protected void AddService<T>(ServiceCreatorCallback callback, bool promote)
            => ((IServiceContainer)this).AddService(typeof(T), callback, promote);

        protected void AddService(Type serviceType, ServiceCreatorCallback callback, bool promote)
            => ((IServiceContainer)this).AddService(serviceType, callback, promote);

        internal static void OpenWebBrowser(System.IServiceProvider serviceProvider, string url)
        {
            // TODO: In a future VS 2017 release, SVsWebBrowsingService will have the ability
            // to open in an external browser, and we may want to switch to using that, as it
            // may be safer/better than Process.Start.
            serviceProvider.GetUIThread().Invoke(() =>
            {
                try
                {
                    var uri = new Uri(url);
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    Utilities.ShowMessageBox(
                       serviceProvider,
                       SR.GetString(SR.WebBrowseNavigateError, url, ex.Message),
                       null,
                       OLEMSGICON.OLEMSGICON_CRITICAL,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
                    );
                }
            });
        }

        internal static void OpenVsWebBrowser(System.IServiceProvider serviceProvider, string url)
        {
            serviceProvider.GetUIThread().Invoke(() =>
            {
                var web = serviceProvider.GetService(typeof(SVsWebBrowsingService)) as IVsWebBrowsingService;
                if (web == null)
                {
                    OpenWebBrowser(serviceProvider, url);
                    return;
                }

                try
                {
                    IVsWindowFrame frame;
                    ErrorHandler.ThrowOnFailure(web.Navigate(url, (uint)__VSWBNAVIGATEFLAGS.VSNWB_ForceNew, out frame));
                    frame.Show();
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    Utilities.ShowMessageBox(
                       serviceProvider,
                       SR.GetString(SR.WebBrowseNavigateError, url, ex.Message),
                       null,
                       OLEMSGICON.OLEMSGICON_CRITICAL,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
                    );
                }
            });
        }

        #region IOleComponent Members

        public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked) => 1;

        public int FDoIdle(uint grfidlef)
        {
            var componentManager = _compMgr;
            if (componentManager == null)
            {
                return 0;
            }

            _libraryManager?.OnIdle(componentManager);
            OnIdle?.Invoke(this, new ComponentManagerEventArgs(componentManager));
            return 0;
        }

        internal event EventHandler<ComponentManagerEventArgs> OnIdle;

        public int FPreTranslateMessage(MSG[] pMsg) => 0;

        public int FQueryTerminate(int fPromptUser) => 1;

        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam) => 1;

        public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved) => IntPtr.Zero;

        public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) { }

        public void OnAppActivate(int fActive, uint dwOtherThreadID) { }

        public void OnEnterState(uint uStateID, int fEnter) { }

        public void OnLoseActivation() { }

        public void Terminate() { }

        #endregion
    }

    internal sealed class ComponentManagerEventArgs : EventArgs
    {
        public ComponentManagerEventArgs(IOleComponentManager compMgr)
        {
            ComponentManager = compMgr;
        }

        public IOleComponentManager ComponentManager { get; }
    }
}

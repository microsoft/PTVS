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

using Microsoft.CookiecutterTools.Commands;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
using Microsoft.CookiecutterTools.Resources;
using Microsoft.CookiecutterTools.Telemetry;
using Microsoft.CookiecutterTools.View;

namespace Microsoft.CookiecutterTools {
    [Guid("AC207EBF-16F8-4AA4-A0A8-70AF37308FCD")]
    sealed class CookiecutterToolWindow : ToolWindowPane, IVsInfoBarUIEvents {
        private IVsUIShell _uiShell;
        private EnvDTE.DTE _dte;

        private CookiecutterContainerPage _cookiecutterPage;
        private IVsInfoBarUIFactory _infoBarFactory;
        private IVsInfoBarUIElement _infoBar;
        private IVsInfoBar _infoBarModel;
        private uint _infoBarAdviseCookie;
        private CookiecutterSessionStartInfo _pendingNewSessionStartInfo;

        private readonly object _commandsLock = new object();
        private readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        public CookiecutterToolWindow(IServiceProvider serviceProvider) : base(serviceProvider) {
            BitmapImageMoniker = ImageMonikers.Cookiecutter;
            Caption = Strings.ToolWindowCaption;
            ToolBar = new CommandID(PackageGuids.guidCookiecutterCmdSet, PackageIds.WindowToolBarId);
        }

        protected override void Dispose(bool disposing) {
            if (_cookiecutterPage != null) {
                _cookiecutterPage.ContextMenuRequested -= OnContextMenuRequested;
            }

            base.Dispose(disposing);
        }

        protected override void OnCreate() {
            base.OnCreate();

            // Show a loading page, delay initialization of the control until
            // VS has created all tool windows (we need the output window).
            var presenter = new Frame() {
                NavigationUIVisibility = NavigationUIVisibility.Hidden,
                Focusable = false,
                IsTabStop = false,
                Content = new LoadingPage(),
            };

            Content = presenter;

            presenter.Dispatcher.InvokeAsync(InitializeContent, DispatcherPriority.ApplicationIdle);
        }

        private void InitializeContent() {
            _uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            _dte = GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            _infoBarFactory = GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;

            if (CookiecutterClientProvider.IsCompatiblePythonAvailable()) {
                ShowCookiecutterPage();
            } else {
                ShowMissingDependenciesPage();
            }

            if (CookiecutterPackage.Instance.ShowHelp) {
                AddInfoBar();
            }

            RegisterCommands(new Command[] {
                new HomeCommand(this),
                new RunCommand(this),
                new UpdateCommand(this),
                new CheckForUpdatesCommand(this),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubHome),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubIssues),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubWiki),
            }, PackageGuids.guidCookiecutterCmdSet);

            RegisterCommands(new Command[] {
                new DeleteInstalledTemplateCommand(this),
            }, VSConstants.GUID_VSStandardCommandSet97);
        }

        private void ShowCookiecutterPage() {
            Debug.Assert(_cookiecutterPage == null);

            var outputWindow = OutputWindowRedirector.GetGeneral(this);
            Debug.Assert(outputWindow != null);

            ReportPrereqsEvent(true);

            string feedUrl = CookiecutterPackage.Instance.RecommendedFeed;
            if (string.IsNullOrEmpty(feedUrl)) {
                feedUrl = UrlConstants.DefaultRecommendedFeed;
            }

            var shell = (IVsShell)GetService(typeof(SVsShell));
            ErrorHandler.ThrowOnFailure(shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out var commonIdeFolderPath));

            var gitClient = GitClientProvider.Create(outputWindow, commonIdeFolderPath as string);
            var projectSystemClient = new ProjectSystemClient((EnvDTE80.DTE2)GetService(typeof(EnvDTE.DTE)));

            _cookiecutterPage = new CookiecutterContainerPage(
                this,
                outputWindow,
                CookiecutterTelemetry.Current,
                gitClient,
                new Uri(feedUrl),
                ExecuteCommand,
                projectSystemClient,
                UpdateCommandUI
            );
            _cookiecutterPage.ContextMenuRequested += OnContextMenuRequested;

            var ssi = _pendingNewSessionStartInfo;
            _pendingNewSessionStartInfo = null;

            _cookiecutterPage.InitializeAsync(CookiecutterPackage.Instance.CheckForTemplateUpdate, ssi).HandleAllExceptions(this, GetType()).DoNotWait();

            ((Frame)Content).Content = _cookiecutterPage;
        }

        private void ShowMissingDependenciesPage() {
            ReportPrereqsEvent(false);

            ((Frame)Content).Content = new MissingDependenciesPage();
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement) {
            if (_infoBar != null) {
                if (_infoBarAdviseCookie != 0) {
                    _infoBar.Unadvise(_infoBarAdviseCookie);
                    _infoBarAdviseCookie = 0;
                }

                // Remember this for next time
                CookiecutterPackage.Instance.ShowHelp = false;

                RemoveInfoBar(_infoBar);
                _infoBar.Close();
                _infoBar = null;
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
            ((Action)actionItem.ActionContext)();
        }

        private void AddInfoBar() {
            Action showHelp = () => Process.Start(UrlConstants.HelpUrl);

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(Strings.InfoBarMessage));
            actions.Add(new InfoBarHyperlink(Strings.InfoBarMessageLink, showHelp));

            _infoBarModel = new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true);
            _infoBar = _infoBarFactory.CreateInfoBar(_infoBarModel);
            AddInfoBar(_infoBar);
            _infoBar.Advise(this, out _infoBarAdviseCookie);
        }

        internal void RegisterCommands(IEnumerable<Command> commands, Guid cmdSet) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                lock (_commandsLock) {
                    foreach (var command in commands) {
                        var beforeQueryStatus = command.BeforeQueryStatus;
                        CommandID toolwndCommandID = new CommandID(cmdSet, command.CommandId);
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);
                        if (beforeQueryStatus != null) {
                            menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        }
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        private void UpdateCommandUI() {
            _uiShell.UpdateCommandUI(0);
        }

        private void ExecuteCommand(string name, string args) {
            try {
                _dte.ExecuteCommand(name, args ?? string.Empty);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
#if !DEV15_OR_LATER
                if (name == "File.OpenFolder") {
                    OpenInWindowsExplorer(args.Trim('"'));
                    return;
                }
#endif
                var outputWindow = OutputWindowRedirector.GetGeneral(this);
                outputWindow.WriteErrorLine(ex.Message);
            }
        }

        internal void NavigateToGitHub(int commandId) {
            switch (commandId) {
                case PackageIds.cmdidLinkGitHubHome:
                    _cookiecutterPage?.NavigateToGitHubHome();
                    break;
                case PackageIds.cmdidLinkGitHubIssues:
                    _cookiecutterPage?.NavigateToGitHubIssues();
                    break;
                case PackageIds.cmdidLinkGitHubWiki:
                    _cookiecutterPage?.NavigateToGitHubWiki();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        internal bool CanNavigateToGitHub() {
            return _cookiecutterPage != null ? _cookiecutterPage.CanNavigateToGitHub() : false;
        }

        internal void Home() {
            if (_cookiecutterPage == null && CookiecutterClientProvider.IsCompatiblePythonAvailable()) {
                // User has installed a compatible python since we first initialized
                ShowCookiecutterPage();
            }

            _cookiecutterPage?.Home();
        }

        internal void DeleteSelection() {
            _cookiecutterPage?.DeleteSelection();
        }

        internal bool CanDeleteSelection() {
            return _cookiecutterPage != null ? _cookiecutterPage.CanDeleteSelection() : false;
        }

        internal void RunSelection() {
            _cookiecutterPage?.RunSelection();
        }

        internal bool CanRunSelection() {
            return _cookiecutterPage != null ? _cookiecutterPage.CanRunSelection() : false;
        }

        internal void CheckForUpdates() {
            _cookiecutterPage?.CheckForUpdates();
        }

        internal bool CanCheckForUpdates() {
            return _cookiecutterPage != null ? _cookiecutterPage.CanCheckForUpdates() : false;
        }

        internal void UpdateSelection() {
            _cookiecutterPage?.UpdateSelection();
        }

        internal bool CanUpdateSelection() {
            return _cookiecutterPage != null ? _cookiecutterPage.CanUpdateSelection() : false;
        }

        internal void NewSession(CookiecutterSessionStartInfo ssi) {
            if (_cookiecutterPage != null) {
                _cookiecutterPage.NewSession(ssi);
            } else {
                // This method may be called immediately after showing the tool
                // window for the first time, which triggers a delayed initialization
                // of the cookiecutter page on idle, causing the page to be temporarily null.
                // Store the desired session start info so that when the page is initialized,
                // it uses it. Doing it in one step in init ensures only
                // one automatic search is triggered.
                _pendingNewSessionStartInfo = ssi;
            }
        }

        private void OnContextMenuRequested(object sender, PointEventArgs e) {
            ShowContextMenu(e.Point);
        }

        private void ShowContextMenu(Point point) {
            CookiecutterPackage.ShowContextMenu(
                new CommandID(PackageGuids.guidCookiecutterCmdSet, PackageIds.ContextMenu),
                (int)point.X,
                (int)point.Y,
                this
            );
        }

        private static void ReportPrereqsEvent(bool found) {
            try {
                CookiecutterTelemetry.Current.TelemetryService.ReportEvent(CookiecutterTelemetry.TelemetryArea.Prereqs, CookiecutterTelemetry.PrereqsEvents.Python, found.ToString());
            } catch (Exception ex) {
                Debug.Fail($"Error reporting event.\n{ex.Message}");
            }
        }
    }
}

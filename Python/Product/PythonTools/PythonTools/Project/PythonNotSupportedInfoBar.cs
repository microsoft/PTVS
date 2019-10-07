using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal abstract class PythonNotSupportedInfoBar : PythonInfoBar {
        private PythonSupportInfoBarData _infoBarData;

        private readonly Version _pythonVersionNotSupported = new Version("3.8");
        protected string _suppressInfoBarPropertyName;
        protected bool _infoBarTriggered = false;

        protected PythonNotSupportedInfoBar(IServiceProvider site) : base(site) {
            _suppressInfoBarPropertyName = string.Concat(
                PythonConstants.SuppressPythonVersionNotSupportedPrompt,
                _pythonVersionNotSupported.ToString().Replace(".", "")
            );
        }

        protected async Task InfoBarCreationRequest(PythonSupportInfoBarData infoBarData) {
            _infoBarData = infoBarData;
            if (IsCreated ||
                _infoBarData.IsInfoBarSuppressed ||
                _infoBarTriggered ||
                _infoBarData.Configuration.Version < _pythonVersionNotSupported
            ) {
                return;
            }

            var infoBarTextSpanMessage = new InfoBarTextSpan(Strings.PythonVersionNotSupportInfoBarText.FormatUI(_infoBarData.Configuration.Version));

            var infoBarMessage = new List<IVsInfoBarTextSpan>() { infoBarTextSpanMessage };
            var actionItems = new List<InfoBarActionItem>() {
                new InfoBarHyperlink(Strings.PythonVersionNotSupportMoreInformation, (Action)MoreInformationAction),
                new InfoBarHyperlink(Strings.DoNotShowMessageAgain, (Action)DoNotShowAgainAction)
            };

            LogEvent(PythonVersionNotSupportedInfoBarAction.Prompt);
            Create(new InfoBarModel(infoBarMessage, actionItems, KnownMonikers.StatusInformation));
            _infoBarTriggered = true;
        }

        private void MoreInformationAction() {
            MoreInformationActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private void DoNotShowAgainAction() {
            DoNotShowAgainActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task MoreInformationActionAsync() {
            LogEvent(PythonVersionNotSupportedInfoBarAction.MoreInfo);
            Close();

            //TODO Raymon

        }

        private async Task DoNotShowAgainActionAsync() {
            LogEvent(PythonVersionNotSupportedInfoBarAction.Ignore);
            Close();

            await SetPropertyAsync(_suppressInfoBarPropertyName, "true");
        }

        private void LogEvent(string action) {
            Logger?.LogEvent(
                PythonLogEvent.PythonNotSupportedInfoBar,
                new PythonVersionNotSupportedInfoBarInfo() {
                    Action = action,
                    Context = _infoBarData.Context,
                    PythonVersion = _infoBarData.Configuration.Version
                }
            );
        }

        protected abstract Task SetPropertyAsync(string propertyName, string propertyValue);

        protected class PythonSupportInfoBarData {
            public InterpreterConfiguration Configuration { get; protected set; }
            public bool IsInfoBarSuppressed { get; }
            public string Context { get; }

            public PythonSupportInfoBarData(InterpreterConfiguration configuration, bool isInfoBarSuppressed, string context) {
                Configuration = configuration;
                IsInfoBarSuppressed = isInfoBarSuppressed;
                Context = context;
            }
        }
    }

    internal sealed class PythonNotSupportedProjectInfoBar : PythonNotSupportedInfoBar {
        private PythonProjectNode _project;

        public PythonNotSupportedProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode) : base(site) {
            _project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));

        }

        public override async Task CheckAsync() {
            PythonSupportInfoBarData infoBarData = new PythonSupportInfoBarData(
                _project.ActiveInterpreter.Configuration,
                _project.GetProjectProperty(_suppressInfoBarPropertyName).IsTrue(),
                InfoBarContexts.Project
                );

            await InfoBarCreationRequest(infoBarData);
        }

        protected override Task SetPropertyAsync(string propertyName, string propertyValue) {
            _project.SetProjectProperty(propertyName, propertyValue);
            return Task.CompletedTask;
        }
    }

    internal sealed class PythonNotSupportedWorkspaceInfoBar : PythonNotSupportedInfoBar {
        private IPythonWorkspaceContext _workspaceContext;

        public PythonNotSupportedWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext pythonWorkspaceContext) : base(site) {
            _workspaceContext = pythonWorkspaceContext ?? throw new ArgumentNullException(nameof(pythonWorkspaceContext));
        }

        public override async Task CheckAsync() {
            PythonSupportInfoBarData infoBarData = new PythonSupportInfoBarData(
                _workspaceContext.CurrentFactory.Configuration,
                _workspaceContext.GetBoolProperty(_suppressInfoBarPropertyName) ?? false,
                InfoBarContexts.Workspace
                );

            await InfoBarCreationRequest(infoBarData);
        }

        protected override async Task SetPropertyAsync(string propertyName, string propertyValue) {
            await _workspaceContext.SetPropertyAsync(propertyName, propertyValue);
        }

    }
}

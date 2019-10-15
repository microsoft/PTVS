using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal class PythonNotSupportedInfoBar : PythonInfoBar {
        private Func<IPythonInterpreterFactory> _getActiveInterpreterFunc;
        private IPythonInterpreterFactory _activeInterpreter { get { return _getActiveInterpreterFunc(); } }

        private GeneralOptions _options;
        private String _context;

        private string _moreInformationLink = @"https://github.com/microsoft/PTVS/issues/1"; //TODO Raymon  Change URL
        private readonly Version _pythonVersionNotSupported = new Version("3.8");
        private bool _infoBarTriggered = false;

        public PythonNotSupportedInfoBar(IServiceProvider site, string context, Func<IPythonInterpreterFactory> activeInterpreter) : base(site) {
            _options = Site.GetPythonToolsService().GeneralOptions;
            _getActiveInterpreterFunc = activeInterpreter;
            _context = context;
        }

        public override Task CheckAsync() {
            if (IsCreated ||
                !_options.PromptForPythonVersionNotSupported ||
                _infoBarTriggered ||
                _activeInterpreter.Configuration.Version < _pythonVersionNotSupported
            ) {
                return Task.CompletedTask;
            }

            var infoBarTextSpanMessage = new InfoBarTextSpan(Strings.PythonVersionNotSupportInfoBarText.FormatUI(_activeInterpreter.Configuration.Version));
            var infoBarMessage = new List<IVsInfoBarTextSpan>() { infoBarTextSpanMessage };
            var actionItems = new List<InfoBarActionItem>() {
                new InfoBarHyperlink(Strings.PythonVersionNotSupportMoreInfo, (Action)MoreInformationAction),
                new InfoBarHyperlink(Strings.PythonVersionNotSupportedDontShowMessageAgain, (Action)DoNotShowAgainAction)
            };

            LogEvent(PythonVersionNotSupportedInfoBarAction.Prompt);
            Create(new InfoBarModel(infoBarMessage, actionItems, KnownMonikers.StatusInformation));
            _infoBarTriggered = true;

            return Task.CompletedTask;
        }

        private void MoreInformationAction() {
            LogEvent(PythonVersionNotSupportedInfoBarAction.MoreInfo);
            Close();

            VsShellUtilities.OpenBrowser(_moreInformationLink);
        }

        private void DoNotShowAgainAction() {
            LogEvent(PythonVersionNotSupportedInfoBarAction.Ignore);
            Close();

            _options.PromptForPythonVersionNotSupported = false;
            _options.Save();
        }

        private void LogEvent(string action) {
            Logger?.LogEvent(
                PythonLogEvent.PythonNotSupportedInfoBar,
                new PythonVersionNotSupportedInfoBarInfo() {
                    Action = action,
                    Context = _context,
                    PythonVersion = _activeInterpreter.Configuration.Version
                }
            );
        }
    }
}

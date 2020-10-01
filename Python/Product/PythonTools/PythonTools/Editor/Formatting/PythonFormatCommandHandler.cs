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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor.Formatting;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Utility;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ICommandHandler))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonFormatCommandHandler))]
    internal class PythonFormatCommandHandler :
        ICommandHandler<FormatDocumentCommandArgs>,
        ICommandHandler<FormatSelectionCommandArgs> {

        private readonly IServiceProvider _site;
        private readonly IEditorOperationsFactoryService _editOperationsFactory;
        private readonly ITextBufferUndoManagerProvider _undoManagerFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly Lazy<IPythonFormatter>[] _formattingProviders;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;

        [ImportingConstructor]
        public PythonFormatCommandHandler(
            [Import(typeof(VisualStudio.Shell.SVsServiceProvider))] IServiceProvider site,
            [Import] IEditorOperationsFactoryService editOperationsFactory,
            [Import] ITextBufferUndoManagerProvider undoManagerFactory,
            [Import] ITextDocumentFactoryService textDocumentFactoryService,
            [Import] JoinableTaskContext joinableTaskContext,
            [ImportMany] Lazy<IPythonFormatter>[] formattingProviders,
            [Import] IInterpreterOptionsService optionsService,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider
        ) {
            _site = site;
            _editOperationsFactory = editOperationsFactory;
            _undoManagerFactory = undoManagerFactory;
            _textDocumentFactoryService = textDocumentFactoryService;
            _joinableTaskFactory = joinableTaskContext.Factory;
            _formattingProviders = formattingProviders;
            _optionsService = optionsService;
            _workspaceContextProvider = workspaceContextProvider;
        }

        public string DisplayName => nameof(PythonFormatCommandHandler);

        public CommandState GetCommandState(FormatDocumentCommandArgs args)
            => CommandState.Available;

        public CommandState GetCommandState(FormatSelectionCommandArgs args) {
            var formatterId = _site.GetPythonToolsService().FormattingOptions.Formatter;
            var formatter = _formattingProviders.FirstOrDefault(x => x.Value.Identifier == formatterId);
            return formatter?.Value.CanFormatSelection == true ? CommandState.Available : new CommandState(true, false, false, true);
        }

        public bool ExecuteCommand(FormatDocumentCommandArgs args, CommandExecutionContext context) {
            return Execute(args.TextView, args.SubjectBuffer, isFormatSelection: false);
        }

        public bool ExecuteCommand(FormatSelectionCommandArgs args, CommandExecutionContext context) {
            return Execute(args.TextView, args.SubjectBuffer, isFormatSelection: true);
        }

        private bool Execute(ITextView textView, ITextBuffer textBuffer, bool isFormatSelection) {
            if (_textDocumentFactoryService.TryGetTextDocument(textBuffer, out var textDoc) &&
                GetConfiguration(textDoc, out var formatter, out var factory, out var extraArgs)) {

                var snapshot = textBuffer.CurrentSnapshot;
                var range = isFormatSelection ? GetRange(textView, textBuffer) : null;

                FormatDocumentAsync(textDoc, snapshot, formatter, factory, range, extraArgs)
                    .HandleAllExceptions(_site, GetType())
                    .DoNotWait();

                //_joinableTaskFactory.RunAsync(async () => {
                //    await FormatDocumentAsync(textDoc, snapshot, formatter, factory, range, extraArgs);
                //}).FileAndForget("vs/python/FormatDocumentFailed"); // TODO

                return true;
            }

            return false;
        }

        private async Task FormatDocumentAsync(ITextDocument textDoc, ITextSnapshot snapshot, IPythonFormatter formatter, IPythonInterpreterFactory factory, Range range, string[] extraArgs) {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var documentFilePath = textDoc.FilePath;
            var documentContents = snapshot.GetText();

            var tempFilePath = textDoc.IsDirty
                ? CreateTempFileWithContents(documentFilePath, documentContents)
                : documentFilePath;

            try {
                bool isErrorModuleNotInstalled;
                bool isErrorInstallingModule;
                do {
                    var isErrorRangeNotSupported = false;
                    isErrorModuleNotInstalled = false;
                    isErrorInstallingModule = false;

                    try {
                        await FormatDocumentAsync(textDoc, snapshot, formatter, tempFilePath, documentContents, range, extraArgs);
                    } catch (Exception e) when (!e.IsCriticalException()) {
                        isErrorModuleNotInstalled = e is PythonFormatterModuleNotFoundException;
                        isErrorRangeNotSupported = e is PythonFormatterRangeNotSupportedException;

                        if (e is PythonFormatterModuleNotFoundException) {
                            isErrorInstallingModule = !await InstallFormatterAsync(formatter, factory);
                        } else {
                            ShowErrorMessage(e.Message);
                        }
                    } finally {
                        stopwatch.Stop();
                        _site.GetPythonToolsService().Logger.LogEvent(
                            PythonLogEvent.FormatDocument,
                            new FormatDocumentInfo {
                                Version = factory.Configuration.Version.ToString(),
                                Formatter = formatter.Identifier,
                                TimeMilliseconds = stopwatch.ElapsedMilliseconds,
                                IsRange = range != null,
                                IsError = true,
                                IsErrorRangeNotSupported = isErrorRangeNotSupported,
                                IsErrorModuleNotInstalled = isErrorModuleNotInstalled,
                                IsErrorInstallingModule = isErrorInstallingModule
                            }
                        );
                    }
                } while (isErrorModuleNotInstalled && !isErrorInstallingModule);
            } finally {
                if (documentFilePath != tempFilePath) {
                    try {
                        File.Delete(tempFilePath);
                    } catch (IOException) { }
                }
            }
        }

        private async Task FormatDocumentAsync(
            ITextDocument textDoc,
            ITextSnapshot snapshot,
            IPythonFormatter formatter,
            string filePath,
            string contents,
            Range range,
            string[] extraArgs
        ) {
            await TaskScheduler.Default;
            var factory = _optionsService.DefaultInterpreter;
            var interpreter = factory.Configuration.InterpreterPath;
            var edits = await formatter.FormatDocumentAsync(interpreter, filePath, contents, range, extraArgs);

            await _joinableTaskFactory.SwitchToMainThreadAsync();
            LspEditorUtilities.ApplyTextEdits(edits, snapshot, textDoc.TextBuffer);
        }

        private async Task<bool> PromptInstallModuleAsync(
            IPythonFormatter formatter,
            IPythonInterpreterFactory factory,
            IPackageManager pm
        ) {
            await _joinableTaskFactory.SwitchToMainThreadAsync();

            var message = Strings.InstallFormatterPrompt.FormatUI(formatter.Package, factory.Configuration.Description);
            if (ShowYesNoPrompt(message)) {
                try {
                    return await pm.InstallAsync(PackageSpec.FromArguments(formatter.Package), new VsPackageManagerUI(_site), CancellationToken.None);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    ShowErrorMessage(Strings.ErrorUnableToInstallFormatter.FormatUI(ex.Message));
                }
            }
            return false;
        }

        private bool GetConfiguration(
            ITextDocument textDoc,
            out IPythonFormatter formatter,
            out IPythonInterpreterFactory factory,
            out string[] extraArgs
        ) {
            formatter = null;
            factory = null;
            extraArgs = new string[0];

            const string defaultFormatterId = "black";
            var workspace = _workspaceContextProvider.Workspace;
            string formatterId = UserSettings.GetStringSetting(PythonConstants.FormatterSetting, textDoc.FilePath, _site, workspace, out var source);
            switch (source) {
                case UserSettings.ValueSource.Project:
                    factory = _site.GetProjectContainingFile(textDoc.FilePath)?.ActiveInterpreter;
                    break;
                case UserSettings.ValueSource.Workspace:
                    factory = workspace.CurrentFactory;
                    break;
            }

            // If all fails, use global setting
            if (string.IsNullOrEmpty(formatterId)) {
                formatterId = _site.GetPythonToolsService().FormattingOptions.Formatter;
                formatterId = !string.IsNullOrEmpty(formatterId) ? formatterId : defaultFormatterId;
            }

            formatter = _formattingProviders.SingleOrDefault(p => string.Compare(p.Value.Identifier, formatterId, StringComparison.OrdinalIgnoreCase) == 0)?.Value;
            factory = factory ?? _optionsService.DefaultInterpreter;

            return formatter != null && factory != null;
        }

        private static Range GetRange(ITextView textView, ITextBuffer textBuffer) {
            var bufferStart = textView.GetPointAtSubjectBuffer(textView.Selection.Start.Position, textBuffer);
            var bufferEnd = textView.GetPointAtSubjectBuffer(textView.Selection.End.Position, textBuffer);

            if (bufferStart.HasValue && bufferEnd.HasValue) {
                return new Range {
                    Start = bufferStart.Value.GetPosition(),
                    End = bufferEnd.Value.GetPosition()
                };
            }

            return null;
        }

        private static string CreateTempFileWithContents(string filePath, string contents) {
            // Don't create file in temp folder since external utilities
            // look into configuration files in the workspace and are not able
            // to find custom rules if file is saved in a random disk location.
            // This means temp file has to be created in the same folder
            // as the original one and then removed.
            var name = Path.GetFileNameWithoutExtension(filePath);
            var tempFilePath = PathUtils.GetAvailableFilename(
                Path.GetDirectoryName(filePath),
                name + "-fmt",
                ".py"
            );
            File.WriteAllText(tempFilePath, contents);
            return tempFilePath;
        }

        private async Task<bool> InstallFormatterAsync(IPythonFormatter formatter, IPythonInterpreterFactory factory) {
            var pm = _optionsService.GetPackageManagers(factory).FirstOrDefault();
            return pm != null && await PromptInstallModuleAsync(formatter, factory, pm);
        }

        private void ShowErrorMessage(string message) {
            var shell = (IVsUIShell)_site.GetService(typeof(SVsUIShell));
            shell.ShowMessageBox(0, Guid.Empty, null, message, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_CRITICAL, 0, out _);
        }

        private bool ShowYesNoPrompt(string message) {
            var shell = (IVsUIShell)_site.GetService(typeof(SVsUIShell));
            shell.ShowMessageBox(0, Guid.Empty, null, message, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_QUERY, 0, out var result);
            return result == NativeMethods.IDYES;
        }
    }
}

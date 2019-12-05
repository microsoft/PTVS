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
using System.Windows;
using Microsoft.PythonTools.Editor.Formatting;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.LanguageServer.Protocol;
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

        public CommandState GetCommandState(FormatSelectionCommandArgs args)
            => CommandState.Available;

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

        private async Task FormatDocumentAsync(
            ITextDocument textDoc,
            ITextSnapshot snapshot,
            IPythonFormatter formatter,
            IPythonInterpreterFactory factory,
            Range range,
            string[] extraArgs
        ) {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var documentFilePath = textDoc.FilePath;
            var documentContents = snapshot.GetText();

            var tempFilePath = textDoc.IsDirty
                ? CreateTempFileWithContents(documentFilePath, documentContents)
                : documentFilePath;

            try {
                await FormatDocumentAsync(
                    textDoc,
                    snapshot,
                    formatter,
                    factory,
                    tempFilePath,
                    documentContents,
                    range,
                    extraArgs
                );

                stopwatch.Stop();

                _site.GetPythonToolsService().Logger.LogEvent(
                    PythonLogEvent.FormatDocument,
                    new FormatDocumentInfo() {
                        Version = factory.Configuration.Version.ToString(),
                        Formatter = formatter.Identifier,
                        TimeMilliseconds = stopwatch.ElapsedMilliseconds,
                        IsRange = range != null,
                    });
            } catch (Exception e) when (!e.IsCriticalException()) {
                stopwatch.Stop();

                var isErrorInstalling = false;
                if (e is PythonFormatterModuleNotFoundException) {
                    var pm = _optionsService.GetPackageManagers(factory).FirstOrDefault();
                    if (pm != null) {
                        isErrorInstalling = (await PromptInstallModuleAsync(formatter, factory, pm)) == false;
                    } else {
                        MessageBox.Show(e.Message, Strings.ProductTitle);
                    }
                } else {
                    MessageBox.Show(e.Message, Strings.ProductTitle);
                }

                _site.GetPythonToolsService().Logger.LogEvent(
                    PythonLogEvent.FormatDocument,
                    new FormatDocumentInfo() {
                        Version = factory.Configuration.Version.ToString(),
                        Formatter = formatter.Identifier,
                        TimeMilliseconds = stopwatch.ElapsedMilliseconds,
                        IsRange = range != null,
                        IsError = true,
                        IsErrorRangeNotSupported = e is PythonFormatterRangeNotSupportedException,
                        IsErrorModuleNotInstalled = e is PythonFormatterModuleNotFoundException,
                        IsErrorInstallingModule = isErrorInstalling,
                    }
                );
            } finally {
                if (documentFilePath != tempFilePath) {
                    try {
                        File.Delete(tempFilePath);
                    } catch (IOException) {
                    }
                }
            }
        }

        private async Task FormatDocumentAsync(
            ITextDocument textDoc,
            ITextSnapshot snapshot,
            IPythonFormatter formatter,
            IPythonInterpreterFactory factory,
            string filePath,
            string contents,
            Range range,
            string[] extraArgs
        ) {
            await TaskScheduler.Default;
            var edits = await formatter.FormatDocumentAsync(
                factory.Configuration.InterpreterPath,
                filePath,
                contents,
                range,
                extraArgs
            );

            await _joinableTaskFactory.SwitchToMainThreadAsync();
            LspEditorUtilities.ApplyTextEdits(edits, snapshot, textDoc.TextBuffer);
        }

        private async Task<bool?> PromptInstallModuleAsync(
            IPythonFormatter formatter,
            IPythonInterpreterFactory factory,
            IPackageManager pm
        ) {
            await _joinableTaskFactory.SwitchToMainThreadAsync();

            // TODO: localization
            var message = "Install '{0}' in environment '{1}' now?".FormatUI(
                formatter.PackageSpec,
                factory.Configuration.Description
            );

            var result = MessageBox.Show(
                message,
                Strings.ProductTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes) {
                try {
                    return await pm.InstallAsync(
                        PackageSpec.FromArguments(formatter.PackageSpec),
                        new VsPackageManagerUI(_site),
                        CancellationToken.None
                    );
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    return false;
                }
            }

            return null;
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
            string formatterId = null;

            if (_workspaceContextProvider.Workspace != null) {
                // Workspace file
                formatterId = _workspaceContextProvider.Workspace.GetStringProperty(PythonConstants.FormatterSetting);
                factory = _workspaceContextProvider.Workspace.CurrentFactory;
            } else {
                var project = _site.GetProjectContainingFile(textDoc.FilePath);
                if (project != null) {
                    // Project file
                    formatterId = project.GetProjectProperty(PythonConstants.FormatterSetting);
                    factory = project.ActiveInterpreter;
                } else {
                    // Loose file
                    formatterId = defaultFormatterId; // TODO: use a global option?
                    factory = _optionsService.DefaultInterpreter;
                }
            }

            // TODO: if not configured, tell the user? for now use default
            if (string.IsNullOrEmpty(formatterId)) {
                formatterId = defaultFormatterId;
            }

            if (!string.IsNullOrEmpty(formatterId)) {
                formatter = _formattingProviders.SingleOrDefault(p => string.Compare(p.Value.Identifier, formatterId, StringComparison.OrdinalIgnoreCase) == 0)?.Value;
            }

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
    }
}

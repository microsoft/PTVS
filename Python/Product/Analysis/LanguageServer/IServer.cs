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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public interface IServer {
        Task<InitializeResult> Initialize(InitializeParams @params);
        Task Initialized(InitializedParams @params);
        Task Shutdown();
        Task Exit();
        void CancelRequest();
        Task DidChangeConfiguration(DidChangeConfigurationParams @params);
        Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params);
        Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params);
        Task<object> ExecuteCommand(ExecuteCommandParams @params);
        Task DidOpenTextDocument(DidOpenTextDocumentParams @params);
        void DidChangeTextDocument(DidChangeTextDocumentParams @params);
        Task WillSaveTextDocument(WillSaveTextDocumentParams @params);
        Task<TextEdit[]> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams @params);
        Task DidSaveTextDocument(DidSaveTextDocumentParams @params);
        Task DidCloseTextDocument(DidCloseTextDocumentParams @params);
        Task<CompletionList> Completion(CompletionParams @params);
        Task<CompletionItem> CompletionItemResolve(CompletionItem item);
        Task<Hover> Hover(TextDocumentPositionParams @params);
        Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params);
        Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params);
        Task<Reference[]> FindReferences(ReferencesParams @params);
        Task<DocumentHighlight[]> DocumentHighlight(TextDocumentPositionParams @params);
        Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params);
        Task<Command[]> CodeAction(CodeActionParams @params);
        Task<CodeLens[]> CodeLens(TextDocumentPositionParams @params);
        Task<CodeLens> CodeLensResolve(CodeLens item);
        Task<DocumentLink[]> DocumentLink(DocumentLinkParams @params);
        Task<DocumentLink> DocumentLinkResolve(DocumentLink item);
        Task<TextEdit[]> DocumentFormatting(DocumentFormattingParams @params);
        Task<TextEdit[]> DocumentRangeFormatting(DocumentRangeFormattingParams @params);
        Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params);
        Task<WorkspaceEdit> Rename(RenameParams @params);

        event EventHandler<ShowMessageEventArgs> OnShowMessage;
        void ShowMessage(MessageType type, string message);

        event EventHandler<LogMessageEventArgs> OnLogMessage;
        void LogMessage(MessageType type, string message);

        event EventHandler<TelemetryEventArgs> OnTelemetry;
        void Telemetry(TelemetryEventArgs e);

        event EventHandler<CommandEventArgs> OnCommand;
        void Command(CommandEventArgs e);

        event EventHandler<RegisterCapabilityEventArgs> OnRegisterCapability;
        Task RegisterCapability(RegistrationParams @params);

        event EventHandler<UnregisterCapabilityEventArgs> OnUnregisterCapability;
        Task UnregisterCapability(UnregistrationParams @params);

        event EventHandler<ApplyWorkspaceEditEventArgs> OnApplyWorkspaceEdit;
        Task<ApplyWorkspaceEditResponse> ApplyWorkspaceEdit(ApplyWorkspaceEditParams @params);

        event EventHandler<PublishDiagnosticsEventArgs> OnPublishDiagnostics;
        void PublishDiagnostics(PublishDiagnosticsEventArgs e);

        Task ReloadModulesAsync(CancellationToken token);
    }

    public interface IServer2 : IServer {
        Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken);
    }
}

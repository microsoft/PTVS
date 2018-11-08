// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudioTools;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LiveShare {
    internal class PythonLanguageServiceProviderCallback : ILanguageServiceProviderCallback {
        private readonly IServiceProvider _serviceProvider;
        private readonly UIThreadBase _uiThread;

        // Cache analyzers for the session to avoid UI thread marshalling
        private readonly ConcurrentDictionary<Uri, VsProjectAnalyzer> _analyzerCache;

        public PythonLanguageServiceProviderCallback(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _uiThread = _serviceProvider.GetUIThread();
            _analyzerCache = new ConcurrentDictionary<Uri, VsProjectAnalyzer>(UriEqualityComparer.Default);
        }

#pragma warning disable 0067
        public event AsyncEventHandler<LanguageServiceNotifyEventArgs> NotifyAsync;
#pragma warning restore 0067

        private async Task<VsProjectAnalyzer> FindAnalyzer(TextDocumentIdentifier document) {
            if (document?.Uri == null) {
                return null;
            }

            if (!_analyzerCache.TryGetValue(document.Uri, out var analyzer)) {
                var filePath = document.Uri.LocalPath;
                if (string.IsNullOrEmpty(filePath)) {
                    return null;
                }

                // TODO: Use URI for more accurate lookup
                analyzer = await _uiThread.InvokeTask(async () =>
                    (await _serviceProvider.FindAllAnalyzersForFile(filePath)).FirstOrDefault() as VsProjectAnalyzer
                );

                analyzer = _analyzerCache.GetOrAdd(document.Uri, analyzer);
            }

            return analyzer;
        }

        public async Task<TOut> RequestAsync<TIn, TOut>(LspRequest<TIn, TOut> method, TIn param, RequestContext context, CancellationToken cancellationToken) {
            if (method.Name == Methods.Initialize.Name) {
                var capabilities = new ServerCapabilities {
                    CompletionProvider = new LSP.CompletionOptions {
                        TriggerCharacters = new[] { "." }
                    },
                    SignatureHelpProvider = new SignatureHelpOptions {
                        TriggerCharacters = new[] { "(", ",", ")" }
                    },
                    HoverProvider = true,
                    DefinitionProvider = true,
                    ReferencesProvider = true
                };
                object result = new InitializeResult { Capabilities = capabilities };
                return (TOut)(result);
            }

            if (method.Name == Methods.TextDocumentCompletion.Name ||
                method.Name == Methods.TextDocumentHover.Name ||
                method.Name == Methods.TextDocumentDefinition.Name ||
                method.Name == Methods.TextDocumentReferences.Name ||
                method.Name == Methods.TextDocumentSignatureHelp.Name
            ) {
                var analyzer = await FindAnalyzer((param as TextDocumentPositionParams)?.TextDocument);
                if (analyzer == null) {
                    return default(TOut);
                }

                if (method.Name == Methods.TextDocumentDefinition.Name) {
                    return (TOut)(object)await analyzer.SendLanguageServerRequestAsync<TIn, Location[]>(method.Name, param);
                }

                return await analyzer.SendLanguageServerRequestAsync<TIn, TOut>(method.Name, param);
            }

            return default(TOut);
        }
    }
}
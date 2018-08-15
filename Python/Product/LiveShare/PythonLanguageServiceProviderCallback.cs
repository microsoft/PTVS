// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Cascade.LanguageServices.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using LS = Microsoft.PythonTools.Analysis.LanguageServer;
using Newtonsoft.Json.Linq;

namespace Microsoft
{
    internal class PythonLanguageServiceProviderCallback : ILanguageServiceProviderCallback
    {
        private SVsServiceProvider serviceProvider;

        public PythonLanguageServiceProviderCallback(SVsServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

#pragma warning disable 0067
        public event AsyncEventHandler<LanguageServiceNotifyEventArgs> NotifyAsync;
#pragma warning restore 0067

        public async Task<TOut> RequestAsync<TIn, TOut>(LspRequest<TIn, TOut> method, TIn param, CancellationToken cancellationToken)
        {
            if (method.Name == Methods.Initialize.Name)
            {
                object capabilities = new ServerCapabilities { CompletionProvider = new VisualStudio.LanguageServer.Protocol.CompletionOptions { TriggerCharacters = new[] { "." } } };
                return (TOut)capabilities;
            }
            if (method.Name == Methods.TextDocumentCompletion.Name)
            {
                var completionParams = param as CompletionParams;
                var filePath = completionParams.TextDocument.Uri.LocalPath;
                VsProjectAnalyzer analyzer = await this.serviceProvider.FindAnalyzerAsync(filePath) as VsProjectAnalyzer;
                var lsCompletionParams = JObject.FromObject(completionParams).ToObject<LS.CompletionParams>();
                object list = await analyzer.SendLanguageServerRequestAsync<LS.CompletionParams, LS.CompletionList>(method.Name, lsCompletionParams);
                return (TOut)list;
            }

            return default(TOut);
        }
    }
}
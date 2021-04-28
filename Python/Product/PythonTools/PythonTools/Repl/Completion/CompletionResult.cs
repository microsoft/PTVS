using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Repl.Completion {
    internal class CompletionResults {
        public bool ResultsAreIncomplete {
            get;
            set;
        }

#pragma warning disable SA1008 // Opening parenthesis should be spaced correctly
        public (ILanguageClient client, LSP.CompletionItem completionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolveFunction)[] Items
#pragma warning restore SA1008 // Opening parenthesis should be spaced correctly
        {
            get;
            set;
        }

        public bool SuggestionMode {
            get;
            set;
        }
    }
}

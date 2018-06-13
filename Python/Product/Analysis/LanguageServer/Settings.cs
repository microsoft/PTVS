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

using System.Threading;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class LanguageServerSettings {
        private int _completionTimeout = Timeout.Infinite;
        public bool SuppressAdvancedMembers { get; set; }
        public int CompletionTimeout => _completionTimeout;

        public void SetCompletionTimeout(int? timeout)
            => _completionTimeout = timeout.HasValue ? timeout.Value : _completionTimeout;
    }

    internal sealed class Capabilities {
        public PythonCapabilities Python { get; }
        public TextDocumentCapabilities TextDocument { get; }

        public Capabilities() {
            Python = new PythonCapabilities();
            TextDocument = new TextDocumentCapabilities();
        }

        public Capabilities(ClientCapabilities capabilities) {
            Python = capabilities.python != null ? new PythonCapabilities(capabilities.python) : new PythonCapabilities();
            TextDocument = capabilities.textDocument != null ? new TextDocumentCapabilities(capabilities.textDocument) : new TextDocumentCapabilities();
        }

        internal class PythonCapabilities {
            public bool AnalysisUpdates { get; }
            public int CompletionsTimeout { get; } = Timeout.Infinite;
            public bool LiveLinting { get; }
            public bool TraceLogging { get; }
            public bool ManualFileLoad { get; }

            public PythonCapabilities() {}

            public PythonCapabilities(PythonClientCapabilities python) {
                if (python.analysisUpdates.HasValue) AnalysisUpdates = python.analysisUpdates.Value;
                if (python.completionsTimeout.HasValue) CompletionsTimeout = python.completionsTimeout.Value;
                if (python.liveLinting.HasValue) LiveLinting = python.liveLinting.Value;
                if (python.manualFileLoad.HasValue) ManualFileLoad = python.manualFileLoad.Value;
                if (python.traceLogging.HasValue) TraceLogging = python.traceLogging.Value;
            }
        }
        
        internal class TextDocumentCapabilities {
            public HoverCapabilities Hover { get; }
            public SignatureHelpCapabilities SignatureHelp { get; }

            public TextDocumentCapabilities() {
                Hover = new HoverCapabilities();
                SignatureHelp = new SignatureHelpCapabilities();
            }

            public TextDocumentCapabilities(TextDocumentClientCapabilities textDocument) {
                Hover = textDocument.hover.HasValue ? new HoverCapabilities(textDocument.hover.Value) : new HoverCapabilities();
                SignatureHelp = textDocument.signatureHelp.HasValue ? new SignatureHelpCapabilities(textDocument.signatureHelp.Value) : new SignatureHelpCapabilities();
            }

            internal class HoverCapabilities {
                public MarkupKind[] ContentFormat { get; }

                public HoverCapabilities() {
                    ContentFormat = new MarkupKind[0];
                }

                public HoverCapabilities(TextDocumentClientCapabilities.HoverCapabilities hover) {
                    ContentFormat = hover.contentFormat ?? new MarkupKind[0];
                }
            }

            internal class SignatureHelpCapabilities {
                public bool SignatureInformationShortLabel { get; }
                public MarkupKind[] SignatureInformationDocumentationFormat { get; }

                public SignatureHelpCapabilities() {
                    SignatureInformationShortLabel = false;
                    SignatureInformationDocumentationFormat = new MarkupKind[0];
                }

                public SignatureHelpCapabilities(TextDocumentClientCapabilities.SignatureHelpCapabilities signatureHelp) {
                    SignatureInformationShortLabel = signatureHelp.signatureInformation?._shortLabel ?? false;
                    SignatureInformationDocumentationFormat = signatureHelp.signatureInformation?.documentationFormat ?? new MarkupKind[0];
                }
            }
        }
    }
}

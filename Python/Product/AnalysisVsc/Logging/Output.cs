// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using LanguageServer.VsCode.Contracts;
using LanguageServer.VsCode.Contracts.Client;
using Microsoft.DsTools.Core.Diagnostics;
using Microsoft.DsTools.Core.Logging;

namespace Microsoft.PythonTools.VsCode.Logging {
    internal sealed class Output : IOutput {
        private readonly IWindow _window;
        private readonly IActionLog _log;

        public Output(IWindow window, IActionLog log) {
            Check.ArgumentNull(nameof(window), window);
            Check.ArgumentNull(nameof(log), log);
            _window = window;
            _log = log;
        }
        public void Write(string text) {
            _window.LogMessage(MessageType.Info, text);
            _log.WriteLine(LogVerbosity.Normal, MessageCategory.General, text);
        }
        public void WriteError(string text) {
            _window.LogMessage(MessageType.Error, text);
            _log.WriteLine(LogVerbosity.Minimal, MessageCategory.Error, text);
        }
    }
}

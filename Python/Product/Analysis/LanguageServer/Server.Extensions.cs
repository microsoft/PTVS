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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    partial class Server {
        public Task LoadExtension(PythonAnalysisExtensionParams extension) => LoadExtension(extension, CancellationToken.None);

        internal async Task LoadExtension(PythonAnalysisExtensionParams extension, CancellationToken cancellationToken) {
            var provider = ActivateObject<ILanguageServerExtensionProvider>(extension.assembly, extension.typeName, null);
            if (provider == null) {
                LogMessage(MessageType.Error, $"Extension provider {extension.assembly} {extension.typeName} failed to load");
                return;
            }

            var ext = await provider.CreateAsync(this, extension.properties ?? new Dictionary<string, object>(), cancellationToken);
            if (ext == null) {
                LogMessage(MessageType.Error, $"Extension provider {extension.assembly} {extension.typeName} returned null");
                return;
            }

            string n = null;
            try {
                n = ext.Name;
            } catch (NotImplementedException) {
            } catch (NotSupportedException) {
            }

            if (!string.IsNullOrEmpty(n)) {
                _extensions.AddOrUpdate(n, ext, (_, previous) => {
                    (previous as IDisposable)?.Dispose();
                    return ext;
                });
            }
        }

        public Task<ExtensionCommandResult> ExtensionCommand(ExtensionCommandParams @params) {
            if (string.IsNullOrEmpty(@params.extensionName)) {
                throw new ArgumentNullException(nameof(@params.extensionName));
            }

            if (!_extensions.TryGetValue(@params.extensionName, out var ext)) {
                throw new LanguageServerException(LanguageServerException.UnknownExtension, "No extension loaded with name: " + @params.extensionName);
            }

            return Task.FromResult(new ExtensionCommandResult {
                properties = ext?.ExecuteCommand(@params.command, @params.properties)
            });
        }
    }
}

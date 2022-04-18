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
using System.IO;
using System.Threading;
using StreamJsonRpc;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class CustomCancellationStrategy : ICancellationStrategy {
        private readonly string _folderName;
        private readonly JsonRpc _jsonRpc;
        private readonly ICancellationStrategy _cancellationStrategy;

        private readonly string _cancellationFolderPath;

        public CustomCancellationStrategy(string folderName, JsonRpc jsonRpc) {
            _folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            _jsonRpc = jsonRpc ?? throw new ArgumentNullException(nameof(jsonRpc));

            _cancellationFolderPath = Path.Combine(Path.GetTempPath(), "python-languageserver-cancellation", _folderName);

            _cancellationStrategy = _jsonRpc.CancellationStrategy;
            _jsonRpc.Disconnected += OnDisconnected;

            try {
                Directory.CreateDirectory(_cancellationFolderPath);
            } catch (Exception e) when (!e.IsCriticalException()) {
                // not much we can do about it.
            }
        }

        public void IncomingRequestStarted(RequestId requestId, CancellationTokenSource cancellationTokenSource) => _cancellationStrategy.IncomingRequestStarted(requestId, cancellationTokenSource);
        public void IncomingRequestEnded(RequestId requestId) => _cancellationStrategy.IncomingRequestEnded(requestId);

        public void CancelOutboundRequest(RequestId requestId) {
            try {
                using (File.OpenWrite(getCancellationFilePath(requestId))) { }
            } catch (Exception e) when (!e.IsCriticalException()) {
                // simply ignore. not that big deal.
            }
        }

        public void OutboundRequestEnded(RequestId requestId) {
            try {
                File.Delete(getCancellationFilePath(requestId));
            } catch (Exception e) when (!e.IsCriticalException()) {
                // simply ignore. not that big deal.
            }
        }

        private string getCancellationFilePath(RequestId requestId) {
            var id = requestId.Number?.ToString() ?? requestId.String ?? "noid";
            return Path.Combine(_cancellationFolderPath, $"cancellation-{id}.tmp");
        }

        private void OnDisconnected(object sender, JsonRpcDisconnectedEventArgs _) {
            // clean up cancellation folder
            try {
                Directory.Delete(_cancellationFolderPath, recursive: true);
            } catch (Exception e) when (!e.IsCriticalException()) {
                // not much we can do. ignore it.
            }
        }
    }
}

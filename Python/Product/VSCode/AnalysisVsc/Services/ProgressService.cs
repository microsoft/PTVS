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

using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis.Infrastructure;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    public sealed class ProgressService : IProgressService {
        private readonly JsonRpc _rpc;
        public ProgressService(JsonRpc rpc) {
            _rpc = rpc;
        }

        public IProgress BeginProgress() => new Progress(_rpc);

        private class Progress : IProgress {
            private readonly JsonRpc _rpc;
            public Progress(JsonRpc rpc) {
                _rpc = rpc;
                _rpc.NotifyAsync("python/beginProgress").DoNotWait();
            }
            public Task Report(string message) => _rpc.NotifyAsync("python/reportProgress", message);
            public void Dispose() => _rpc.NotifyAsync("python/endProgress").DoNotWait();
        }
    }
}

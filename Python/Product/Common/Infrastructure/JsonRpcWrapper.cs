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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using StreamJsonRpc;

namespace Microsoft.PythonTools.Common.Infrastructure {
    internal readonly struct JsonRpcWrapper {

        private readonly JsonRpc _rpc;

        // list of exception types to ignore
        private static readonly Type[] _exceptionsToIgnore = {
            typeof(ObjectDisposedException),
            typeof(ConnectionLostException)
        };

        public JsonRpcWrapper(JsonRpc rpc) {
            _rpc = rpc;
        }

        public Task NotifyWithParameterObjectAsync(string targetName, object argument = null) {

            if (_rpc is null) {
                return Task.CompletedTask;
            }

            // if the underlying rpc connection was disposed, or the connection was lost, ignore the errors
            return _rpc.NotifyWithParameterObjectAsync(targetName, argument)
                .SilenceExceptions(_exceptionsToIgnore);
        }

        public Task<T> InvokeWithParameterObjectAsync<T>(string request, object parameters, CancellationToken t) {

            if (_rpc is null) {
                return null;
            }

            // if the underlying rpc connection was disposed, or the connection was lost, ignore the errors
            return _rpc.InvokeWithParameterObjectAsync<T>(request, parameters, t)
                .SilenceExceptions(_exceptionsToIgnore);
        }
    }
}

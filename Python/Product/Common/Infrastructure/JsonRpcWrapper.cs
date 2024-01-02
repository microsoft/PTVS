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
    internal static class JsonRpcExtensions {

        private static readonly Type[] _exceptionsToIgnore = {
            typeof(ObjectDisposedException),
            typeof(ConnectionLostException)
        };

        public static Task NotifyWithParameterObjectAsync(this JsonRpc rpc, string targetName, object argument = null) {

            if (rpc is null) {
                return Task.CompletedTask;
            }

            return rpc.NotifyWithParameterObjectAsync(targetName, argument)
                .SilenceExceptions(_exceptionsToIgnore);
        }

        public static Task<T> InvokeWithParameterObjectAsync<T>(this JsonRpc rpc, string request, object parameters, CancellationToken t) {

            if (rpc is null) {
                return null;
            }

            return rpc.InvokeWithParameterObjectAsync<T>(request, parameters, t)
                .SilenceExceptions(_exceptionsToIgnore);
        }
    }
}

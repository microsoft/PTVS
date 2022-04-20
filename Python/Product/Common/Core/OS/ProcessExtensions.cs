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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Threading;

namespace Microsoft.PythonTools.Common.Core.OS {
    public static class ProcessExtensions {
        public static Task WaitForExitAsync(this IProcess process, int milliseconds, CancellationToken cancellationToken = default(CancellationToken)) {
            var tcs = new TaskCompletionSource<int>();
            process.Exited += (o, e) => tcs.TrySetResult(0);
            tcs.RegisterForCancellation(milliseconds, cancellationToken).UnregisterOnCompletion(tcs.Task);
            return tcs.Task;
        }
    }
}

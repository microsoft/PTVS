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

namespace Microsoft.PythonTools.Common.Core.Threading {
    public static class CancellationTokenUtilities {
        public static void UnregisterOnCompletion(this CancellationTokenRegistration registration, Task task) 
            => task.ContinueWith(UnregisterCancellationToken, registration, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        private static void UnregisterCancellationToken(Task task, object state) => ((CancellationTokenRegistration)state).Dispose();
    }
}

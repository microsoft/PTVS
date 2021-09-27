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

namespace DebuggerTests
{
    internal static class TastExtensions
    {
        public static async Task<T> CancelAfter<T>(int milliseconds, string message = null)
        {
            message = message ?? $"Timed out after {milliseconds} ms";
            await Task.Delay(Debugger.IsAttached ? Timeout.Infinite : milliseconds);
            throw new TaskCanceledException(message);
        }

        public static Task CancelAfter(int milliseconds, string message = null) =>
            CancelAfter<object>(milliseconds, message);

        public static Task WithTimeout(this Task task, int milliseconds, string message = null) =>
            Task.WhenAny(task, CancelAfter(milliseconds, message)).Unwrap();

        public static Task<T> WithTimeout<T>(this Task<T> task, int milliseconds, string message = null) =>
            Task.WhenAny(task, CancelAfter<T>(milliseconds, message)).Unwrap();
    }
}

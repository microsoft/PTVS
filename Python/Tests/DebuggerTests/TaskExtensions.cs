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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using TestUtilities;

namespace DebuggerTests {
    internal static class TastExtensions {
        public static async Task<T> CancelAfter<T>(int milliseconds, string message = null) {
            message = message ?? $"Timed out after {milliseconds} ms";
            await Task.Delay(milliseconds);
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

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

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    class TestPackageManagerUI : IPackageManagerUI {
        public void OnErrorTextReceived(string text) {
            Trace.TraceError(text);
        }

        public void OnOperationFinished(string operation, bool success) {
            Trace.TraceInformation("{0} finished. Success: {1}", operation, success);
        }

        public void OnOperationStarted(string operation) {
            Trace.TraceInformation("{0} started.", operation);
        }

        public void OnOutputTextReceived(string text) {
            Trace.TraceInformation(text);
        }

        public Task<bool> ShouldElevateAsync(string operation) {
            return Task.FromResult(false);
        }
    }
}

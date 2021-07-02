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

using System.Diagnostics;
using System.Threading.Tasks;

namespace TestUtilities.Python {
    public class TestPackageManagerUI : IPackageManagerUI {
        private static string RemoveNewline(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            if (text[text.Length - 1] == '\n') {
                if (text.Length >= 2 && text[text.Length - 2] == '\r') {
                    return text.Remove(text.Length - 2);
                }
                return text.Remove(text.Length - 1);
            }
            return text;
        }

        public void OnOutputTextReceived(IPackageManager sender, string text) {
            Trace.TraceInformation(RemoveNewline(text));
        }

        public void OnErrorTextReceived(IPackageManager sender, string text) {
            Trace.TraceError(RemoveNewline(text));
        }

        public void OnOperationFinished(IPackageManager sender, string operation, bool success) {
            Trace.TraceInformation("{0} finished. Success: {1}", operation, success);
        }

        public void OnOperationStarted(IPackageManager sender, string operation) {
            Trace.TraceInformation("{0} started.", operation);
        }

        public Task<bool> ShouldElevateAsync(IPackageManager sender, string operation) {
            return Task.FromResult(false);
        }
    }
}

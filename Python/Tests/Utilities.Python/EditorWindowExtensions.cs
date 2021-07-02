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

using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI.Python {
    static class EditorWindowExtensions {
        public static async Task<VsProjectAnalyzer> WaitForAnalyzerAtCaretAsync(this EditorWindow doc) {
            for (int i = 0; i < 100; i++) {
                var analyzer = doc.TextView.GetAnalyzerAtCaret(doc.VisualStudioApp.ServiceProvider);
                if (analyzer != null) {
                    return analyzer;
                }
                await Task.Delay(100);
            }

            Assert.Fail("Timed out waiting for analyzer");
            return null;
        }

        public static async Task WaitForAnalysisAtCaretAsync(this EditorWindow doc) {
            for (int i = 0; i < 100; i++) {
                var analysis = doc.TextView.GetAnalysisAtCaret(doc.VisualStudioApp.ServiceProvider);
                if (analysis != null && analysis.IsAnalyzed) {
                    return;
                }
                await Task.Delay(100);
            }

            Assert.Fail("Timed out waiting for analyzer");
        }
    }
}

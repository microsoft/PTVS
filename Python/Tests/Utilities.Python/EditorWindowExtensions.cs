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

using Microsoft.PythonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.VSTestHost;

namespace TestUtilities.UI.Python {
    static class EditorWindowExtensions {
        public static void WaitForAnalyzerAtCaret(this EditorWindow doc) {
            for (int i = 0; i < 100; i++) {
                var analyzer = doc.TextView.GetAnalyzerAtCaret(VSTestContext.ServiceProvider);
                if (analyzer != null) {
                    return;
                }
                System.Threading.Thread.Sleep(100);
            }

            Assert.Fail("Timed out waiting for analyzer");
        }

        public static void WaitForAnalysisAtCaret(this EditorWindow doc) {
            for (int i = 0; i < 100; i++) {
                var analysis = doc.TextView.GetAnalysisAtCaret(VSTestContext.ServiceProvider);
                if (analysis != null && analysis.IsAnalyzed) {
                    return;
                }
                System.Threading.Thread.Sleep(100);
            }

            Assert.Fail("Timed out waiting for analyzer");
        }
    }
}

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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.TestAdapter.Utils {
    static class TestUtils {

        // For a small set of tests, we'll pass them on the command
        // line. Once we exceed a certain (arbitrary) number, create
        // a test list on disk so that we do not overflow the 
        // 32K argument limit.
        internal static string CreateTestList(IEnumerable<string> tests) {
            var testList = Path.GetTempFileName();
            using (var writer = new StreamWriter(testList, false, new UTF8Encoding(false))) {
                foreach (var test in tests) {
                    writer.WriteLine(test);
                }
            }
            return testList;
        }
    }
}

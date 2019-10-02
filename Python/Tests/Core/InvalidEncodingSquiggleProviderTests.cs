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

extern alias pythontools;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Classification;
using pythontools::Microsoft.PythonTools.Intellisense;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class InvalidEncodingSquiggleProviderTests {
        #region Test Cases

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void EncodingValidation() {
            // https://www.python.org/dev/peps/pep-0263/
            RunTest(string.Empty, Encoding.UTF8, null, 0);
            RunTest(string.Empty, Encoding.ASCII, null, 0, 
                "UTF-8", "does not match", Encoding.ASCII.EncodingName);

            RunTest("# -*- coding: utf-8 -*-", Encoding.UTF8, "utf-8", 14);
            RunTest("# -*- coding: uf-8 -*-", Encoding.UTF8, "uf-8", 14, "not recognized");
            RunTest("# -*- coding: utf-8 -*-", Encoding.ASCII, "utf-8", 14,
                "utf-8", "coding comment", "does not match", Encoding.ASCII.EncodingName);

            RunTest("#\r\n# -*- coding: utf-8 -*-", Encoding.UTF8, "utf-8", 17);
            RunTest("#\r\n# -*- coding: uf-8 -*-", Encoding.UTF8, "uf-8", 17, "not recognized");
            RunTest("#\r\n# -*- coding: utf-8 -*-", Encoding.ASCII, "utf-8", 17,
                "utf-8", "coding comment", "does not match", Encoding.ASCII.EncodingName);
        }

        private void RunTest(string content, Encoding fileEncoding, string expectedMagicEncodingName, int expectedMagicEncodingIndex, params string[] messageContains) {
            var snapshot = new MockTextSnapshot(new MockTextBuffer(content), content);
            var message = InvalidEncodingSquiggleProvider.CheckEncoding(snapshot, fileEncoding, out var magicEncodingName, out var magicEncodingIndex);
            if (messageContains.Length > 0) {
                AssertUtil.Contains(message, messageContains);
            } else {
                Assert.IsNull(message, message);
            }
            Assert.AreEqual(expectedMagicEncodingName, magicEncodingName);
            Assert.AreEqual(expectedMagicEncodingIndex, magicEncodingIndex);
        }
        #endregion Test Cases
    }
}
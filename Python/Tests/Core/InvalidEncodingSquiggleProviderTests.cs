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
using System.Text;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Classification;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class InvalidEncodingSquiggleProviderTests {
        #region Test Cases

        [TestMethod, Priority(0)]
        public void EncodingValidation() {
            // https://www.python.org/dev/peps/pep-0263/
            RunTest(string.Empty, Encoding.UTF8, null, 0, m => m == null);
            RunTest(string.Empty, Encoding.ASCII, null, 0, 
                m => m.Contains("UTF-8") && m.Contains("does not match") && m.Contains(Encoding.ASCII.EncodingName));

            RunTest("# -*- coding: utf-8 -*-", Encoding.UTF8, "utf-8", 14, m => m == null);
            RunTest("# -*- coding: uf-8 -*-", Encoding.UTF8, "uf-8", 14, m => m.Contains("not recognized"));
            RunTest("# -*- coding: utf-8 -*-", Encoding.ASCII, "utf-8", 14,
                m => m.Contains("utf-8") && m.Contains("magic") && m.Contains("does not match") && m.Contains(Encoding.ASCII.EncodingName));

            RunTest("#\r\n# -*- coding: utf-8 -*-", Encoding.UTF8, "utf-8", 17, m => m == null);
            RunTest("#\r\n# -*- coding: uf-8 -*-", Encoding.UTF8, "uf-8", 17, m => m.Contains("not recognized"));
            RunTest("#\r\n# -*- coding: utf-8 -*-", Encoding.ASCII, "utf-8", 17,
                m => m.Contains("utf-8") && m.Contains("magic") && m.Contains("does not match") && m.Contains(Encoding.ASCII.EncodingName));
        }

        private void RunTest(string content, Encoding fileEncoding, string expectedMagicEncodingName, int expectedMagicEncodingIndex, Func<string, bool> verifyMessage) {
            var snapshot = new MockTextSnapshot(new MockTextBuffer(content), content);
            var message = InvalidEncodingSquiggleProvider.CheckEncoding(snapshot, fileEncoding, out var magicEncodingName, out var magicEncodingIndex);
            Assert.IsTrue(verifyMessage(message));
            Assert.AreEqual(expectedMagicEncodingName, magicEncodingName);
            Assert.AreEqual(expectedMagicEncodingIndex, magicEncodingIndex);
        }
        #endregion Test Cases
    }
}
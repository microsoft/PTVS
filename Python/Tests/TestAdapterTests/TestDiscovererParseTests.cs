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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAdapterTests {
    [TestClass]
    public class TestDiscovererParseTests {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PytestShouldThrowNullResults() {
            var discoverer = new PytestTestDiscoverer();
            var testcases = discoverer.ParseDiscoveryResults(null, projectHome: "dummypath");
            testcases.Any();
        }

        [TestMethod]
        public void PytestShouldHandleEmptyListResults() {
            var discoverer = new PytestTestDiscoverer();
            var results = new List<PytestDiscoveryResults>();
            var testcases = discoverer.ParseDiscoveryResults(results, projectHome: "dummypath");

            Assert.IsFalse(testcases.Any());
        }

        [TestMethod]
        public void PytestShouldHandleEmptyResults() {
            var discoverer = new PytestTestDiscoverer();
            var results = new List<PytestDiscoveryResults>() { new PytestDiscoveryResults() };
            var testcases = discoverer.ParseDiscoveryResults(results, projectHome: "dummypath");

            Assert.IsFalse(testcases.Any());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UnnitShouldThrowNullResults() {
            var discoverer = new UnittestTestDiscoverer();
            var testcases = discoverer.ParseDiscoveryResults(null, projectHome: "dummypath");
            testcases.Any();
        }

        [TestMethod]
        public void UnittestShouldHandleEmptyListResults() {
            var discoverer = new UnittestTestDiscoverer();
            var results = new List<UnittestDiscoveryResults>();
            var testcases = discoverer.ParseDiscoveryResults(results, projectHome: "dummypath");

            Assert.IsFalse(testcases.Any());
        }

        [TestMethod]
        public void UnittestShouldHandleEmptyResults() {
            var discoverer = new UnittestTestDiscoverer();
            var results = new List<UnittestDiscoveryResults>() { new UnittestDiscoveryResults() };
            var testcases = discoverer.ParseDiscoveryResults(results, projectHome: "dummypath");

            Assert.IsFalse(testcases.Any());
        }
    }
}

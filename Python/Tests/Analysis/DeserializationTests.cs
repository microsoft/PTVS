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

using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class DeserializationTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(1)]
        public void TestSimpleDeserialize() {
            object obj;
            using (var stream = new FileStream(TestData.GetPath(@"TestData\empty_dict.pickle"), FileMode.Open, FileAccess.Read)) {
                obj = Unpickle.Load(stream);
            }

            Assert.AreEqual(obj.GetType(), typeof(Dictionary<string, object>));
            Assert.AreEqual(((Dictionary<string, object>)obj).Count, 0);

            using (var stream = new FileStream(TestData.GetPath(@"TestData\simple_dict.pickle"), FileMode.Open, FileAccess.Read)) {
                obj = Unpickle.Load(stream);
            }

            Assert.AreEqual(obj.GetType(), typeof(Dictionary<string, object>));
            var dict = ((Dictionary<string, object>)obj);
            Assert.AreEqual(dict.Count, 8);

            Assert.AreEqual(dict["int"].GetType(), typeof(int)); Assert.AreEqual(dict["int"], 42);
            Assert.AreEqual(dict["str"].GetType(), typeof(string)); Assert.AreEqual(dict["str"], "baz");
            Assert.AreEqual(dict["long"].GetType(), typeof(BigInteger)); Assert.AreEqual(dict["long"], (BigInteger)42);
            Assert.AreEqual(dict["float"].GetType(), typeof(double)); Assert.AreEqual(dict["float"], 42.0);
            Assert.AreEqual(dict["emptytuple"].GetType(), typeof(object[])); Assert.AreEqual(((object[])dict["emptytuple"]).Length, 0);
            Assert.AreEqual(dict["tuple"].GetType(), typeof(object[]));
            Assert.AreEqual(dict["emptylist"].GetType(), typeof(List<object>));
            Assert.AreEqual(dict["list"].GetType(), typeof(List<object>));

            using (var stream = new FileStream(TestData.GetPath(@"TestData\simple_dict2.pickle"), FileMode.Open, FileAccess.Read)) {
                obj = Unpickle.Load(stream);
            }

            Assert.AreEqual(obj.GetType(), typeof(Dictionary<string, object>));
            dict = ((Dictionary<string, object>)obj);
            Assert.AreEqual(dict.Count, 7);

            Assert.AreEqual(dict["true"].GetType(), typeof(bool));  Assert.AreEqual(dict["true"], true);
            Assert.AreEqual(dict["false"].GetType(), typeof(bool)); Assert.AreEqual(dict["false"], false);
            Assert.AreEqual(dict["None"], null);
            Assert.AreEqual(dict["tuple1"].GetType(), typeof(object[])); Assert.AreEqual(((object[])dict["tuple1"])[0], 42);
            Assert.AreEqual(dict["tuple2"].GetType(), typeof(object[])); Assert.AreEqual(((object[])dict["tuple2"])[0], 42); Assert.AreEqual(((object[])dict["tuple2"])[1], 42);
            Assert.AreEqual(dict["tuple3"].GetType(), typeof(object[])); Assert.AreEqual(((object[])dict["tuple3"])[0], 42); Assert.AreEqual(((object[])dict["tuple3"])[1], 42); Assert.AreEqual(((object[])dict["tuple3"])[2], 42);
            Assert.AreEqual(dict["unicode"].GetType(), typeof(string)); Assert.AreEqual(dict["unicode"], "abc");

        }
    }
}

/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void TestSimpleDeserialize() {
            var obj = Unpickle.Load(TestData.Open(@"TestData\empty_dict.pickle"));

            Assert.AreEqual(obj.GetType(), typeof(Dictionary<string, object>));
            Assert.AreEqual(((Dictionary<string, object>)obj).Count, 0);

            obj = Unpickle.Load(TestData.Open(@"TestData\simple_dict.pickle"));

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

            obj = Unpickle.Load(TestData.Open(@"TestData\simple_dict2.pickle"));

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

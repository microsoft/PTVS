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

using System;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class CPythonInterpreterTests {
        internal static readonly CPythonInterpreterFactoryProvider InterpFactory = new CPythonInterpreterFactoryProvider();

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }
        
        [TestMethod, Priority(0)]
        public void FactoryProvider() {
            var provider = InterpFactory;
            var factories = provider.GetInterpreterFactories().ToArray();

            foreach (var factory in factories) {
                if (factory.Id == new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}")) {
                    Assert.IsTrue(factory.Description == "Python");
                    Assert.IsTrue(factory.Configuration.Version.Major == 2 || factory.Configuration.Version.Major == 3);
                    Assert.IsTrue(factory.CreateInterpreter() != null);
                } else if (factory.Id == new Guid("{9A7A9026-48C1-4688-9D5D-E5699D47D074}")) {
                    Assert.IsTrue(factory.Description == "Python 64-bit");
                    Assert.IsTrue(factory.Configuration.Version.Major == 2 || factory.Configuration.Version.Major == 3);
                    Assert.IsTrue(factory.CreateInterpreter() != null);
                } else {
                    Assert.Fail("Expected Id == {2AF0F10D-7135-4994-9156-5D01C9C11B7E} or {9A7A9026-48C1-4688-9D5D-E5699D47D074}");
                }
            }
        }
    }
}

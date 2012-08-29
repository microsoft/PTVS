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
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PythonToolsTests {
    [TestClass]
    public class CPythonInterpreterTests {
        internal static readonly CPythonInterpreterFactoryProvider InterpFactory = new CPythonInterpreterFactoryProvider();

        [TestMethod, Priority(0)]
        public void FactoryProvider() {
            var provider = InterpFactory;
            var factories = provider.GetInterpreterFactories().ToArray();

            if (factories.Length > 0) {
                Assert.IsTrue(factories[0].Description == "Python");
                Assert.IsTrue(factories[0].Configuration.Version.Major == 2 || factories[0].Configuration.Version.Major == 3);
                Assert.IsTrue(factories[0].Id == new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}"));
                Assert.IsTrue(factories[0].CreateInterpreter() != null);
            }
        }
    }
}

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
using System.Globalization;
using System.Linq;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsUITests {
    [TestClass]
    public class InterpreterListTests {
        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void GetInstalledInterpreters() {
            var interps = InterpreterView.GetInterpreters().ToList();
            foreach (var ver in PythonPaths.Versions) {
                var expected = string.Format(CultureInfo.InvariantCulture, "{0};{1}", ver.Interpreter, ver.Version);
                Assert.AreEqual(1, interps.Count(iv => iv.Identifier.Equals(expected, StringComparison.Ordinal)), expected);
            }
        }
    }
}

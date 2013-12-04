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
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PythonToolsTests {
    [TestClass]
    public class LauncherTests {
        [TestMethod, Priority(0)]
        public void LaunchWebBrowserUriTests() {
            var testCases = new[] {
                new { Url = "/fob", Port = 0, Expected = "http://localhost/fob" },
                new { Url = "http://localhost:9999/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
                new { Url = "http://localhost/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
                new { Url = "fob", Port = 9999, Expected = "http://localhost:9999/fob" },
            };

            foreach(var testCase in testCases) {
                Console.WriteLine("{0} {1} == {2}", testCase.Url, testCase.Port, testCase.Expected);

                Assert.AreEqual(
                    PythonWebLauncher.GetFullUrl(testCase.Url, testCase.Port),
                    testCase.Expected
                );
            }
        }
    }
}

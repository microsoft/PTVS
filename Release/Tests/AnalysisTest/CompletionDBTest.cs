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
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem(@"..\\PythonTools\\PythonScraper.py")]
    [DeploymentItem(@"..\\PythonTools\\BuiltinScraper.py")]
    [DeploymentItem(@"..\\PythonTools\\IronPythonScraper.py")]
    [DeploymentItem("PyDebugAttach.dll")]
    public class CompletionDBTest {
        [TestMethod]
        public void TestOpen() {
            Guid testId = Guid.NewGuid();
            var testDir = Path.Combine(Path.GetTempPath(), testId.ToString());
            Directory.CreateDirectory(testDir);

            // run the scraper
            var startInfo = new ProcessStartInfo("C:\\Python27\\python.exe", 
                String.Format("PythonScraper.py \"{0}\" \"{1}\"", testDir, Path.Combine(Directory.GetCurrentDirectory(), "CompletionDB"))
            );

            var process = Process.Start(startInfo);
            process.WaitForExit();

            // it should succeed
            Assert.AreEqual(process.ExitCode, 0);

            // perform some basic validation
            dynamic builtinDb = Unpickle.Load(new FileStream(Path.Combine(testDir, "__builtin__.idb"), FileMode.Open, FileAccess.Read));
            foreach (var overload in builtinDb["members"]["open"]["value"]["overloads"]) {
                Assert.AreEqual(overload["ret_type"][0], "__builtin__");
                Assert.AreEqual(overload["ret_type"][1], "file");
            }
        }
    }
}

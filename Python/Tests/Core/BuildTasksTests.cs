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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class BuildTasksTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void TestResolveEnvironment() {
            var proj1 = new ProjectInstance(TestData.GetPath(@"TestData\Targets\Environments1.pyproj"));
            Assert.IsTrue(proj1.Build("TestResolveEnvironment", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed) }));

            var proj2 = new ProjectInstance(TestData.GetPath(@"TestData\Targets\Environments2.pyproj"));
            Assert.IsTrue(proj2.Build("TestResolveEnvironment", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed) }));
        }

        [TestMethod, Priority(0)]
        public void TestResolveEnvironmentReference() {
            var proj = new ProjectInstance(TestData.GetPath(@"TestData\Targets\EnvironmentReferences1.pyproj"));
            Assert.IsTrue(proj.Build("TestResolveEnvironment", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed) }));
        }

        [TestMethod, Priority(0)]
        public void TestCommandDefinitions() {
            var proj = new ProjectInstance(TestData.GetPath(@"TestData\Targets\Commands1.pyproj"));
            Assert.IsTrue(proj.Build("TestCommands", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed) }));
        }
    }
}

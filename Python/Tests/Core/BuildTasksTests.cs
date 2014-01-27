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
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Parsing;
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

        [TestMethod, Priority(0)]
        public void TestRunPythonCommand() {
            var expectedSearchPath = string.Format("['{0}', '{1}']",
                TestData.GetPath(@"TestData").Replace("\\", "\\\\"),
                TestData.GetPath(@"TestData\HelloWorld").Replace("\\", "\\\\")
            );

            var proj = new Project(TestData.GetPath(@"TestData\Targets\Commands4.pyproj"));

            foreach (var version in PythonPaths.Versions) {
                var verStr = version.Version.ToVersion().ToString();
                proj.SetProperty("InterpreterId", version.Interpreter.ToString("B"));
                proj.SetProperty("InterpreterVersion", verStr);
                proj.RemoveItems(proj.ItemsIgnoringCondition.Where(i => i.ItemType == "InterpreterReference").ToArray());
                proj.AddItem("InterpreterReference", string.Format("{0:B}\\{1}", version.Interpreter, verStr));
                proj.Save();
                proj.ReevaluateIfNecessary();

                var log = new StringLogger(LoggerVerbosity.Minimal);
                Assert.IsTrue(proj.Build("CheckCode", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed), log }));
                
                Console.WriteLine();
                Console.WriteLine("Output from {0:B} {1}", version.Interpreter, version.Version.ToVersion());
                foreach (var line in log.Lines) {
                    Console.WriteLine("* {0}", line.TrimEnd('\r', '\n'));
                }

                var logLines = log.Lines.Last().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(2, logLines.Length);
                Assert.AreEqual(version.Version.ToVersion().ToString(), logLines[0].Trim());
                Assert.AreEqual(expectedSearchPath, logLines[1].Trim());
            }
        }

        class StringLogger : Microsoft.Build.Logging.ConsoleLogger {
            public readonly List<string> Lines = new List<string>();

            public StringLogger(LoggerVerbosity verbosity)
                : base(verbosity) {
                WriteHandler = Lines.Add;
            }
        }
    }
}

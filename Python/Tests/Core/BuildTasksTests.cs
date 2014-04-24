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

extern alias analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using CommonUtils = analysis::Microsoft.VisualStudioTools.CommonUtils;

namespace PythonToolsTests {
    [TestClass]
    public class BuildTasksTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void TestResolveProjectHome() {
            var proj = ProjectRootElement.Create();
            var g = proj.AddPropertyGroup();
            var projectHome = g.AddProperty("ProjectHome", "");
            var expected = g.AddProperty("Expected", "");
            g.AddProperty("StartupFile", "app.py");
            g.AddProperty("_PythonToolsPath", TestData.GetPath(""));

            proj.AddImport(TestData.GetPath("Microsoft.PythonTools.targets"));
            
            var target = proj.AddTarget("TestOutput");
            foreach (var variable in new[] { "ProjectHome", "QualifiedProjectHome", "StartupFile", "StartupPath", "Expected" }) {
                var task = target.AddTask("Message");
                task.SetParameter("Importance", "high");
                task.SetParameter("Text", string.Format("{0} = $({0})", variable));
            }
            var errTask = target.AddTask("Error");
            errTask.Condition = "$(Expected) != $(QualifiedProjectHome)";
            errTask.SetParameter("Text", "Expected did not match QualifiedProjectHome");

            
            var loc = CommonUtils.EnsureEndSeparator(TestData.GetTempPath(randomSubPath: true));
            proj.Save(Path.Combine(loc, string.Format("test.proj")));

            foreach(var test in new [] {
                new { ProjectHome="", Expected=loc },
                new { ProjectHome=".", Expected=loc },
                new { ProjectHome="..", Expected=CommonUtils.EnsureEndSeparator(Path.GetDirectoryName(Path.GetDirectoryName(loc))) },
                new { ProjectHome="\\", Expected=Directory.GetDirectoryRoot(loc) },
                new { ProjectHome="abc", Expected=loc + @"abc\" },
                new { ProjectHome=@"a\b\c", Expected=loc + @"a\b\c\" },
                new { ProjectHome=@"a\b\..\c", Expected=loc + @"a\c\" },
            }) {
                projectHome.Value = test.ProjectHome;
                expected.Value = test.Expected;
                var inst = new ProjectInstance(proj);
                Assert.IsTrue(inst.Build("TestOutput", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed) }));
            }
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
                proj.SetProperty("InterpreterId", version.Id.ToString("B"));
                proj.SetProperty("InterpreterVersion", verStr);
                proj.RemoveItems(proj.ItemsIgnoringCondition.Where(i => i.ItemType == "InterpreterReference").ToArray());
                proj.AddItem("InterpreterReference", string.Format("{0:B}\\{1}", version.Id, verStr));
                proj.Save();
                proj.ReevaluateIfNecessary();

                var log = new StringLogger(LoggerVerbosity.Minimal);
                Assert.IsTrue(proj.Build("CheckCode", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Detailed), log }));
                
                Console.WriteLine();
                Console.WriteLine("Output from {0:B} {1}", version.Id, version.Version.ToVersion());
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

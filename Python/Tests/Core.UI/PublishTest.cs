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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

extern alias pythontools;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using CommonUtils = pythontools::Microsoft.VisualStudioTools.CommonUtils;

namespace PythonToolsUITests {
    public class PublishTest {
        private const string TestSharePublic = "\\\\pytools\\Test$";

        public TestContext TestContext { get; set; }

        [DllImport("mpr")]
        static extern uint WNetCancelConnection2(string lpName, uint dwFlags, bool fForce);

        private static string[] WaitForFiles(string dir) {
            string[] confirmation = null;
            string[] files = null;
            for (int retries = 10; retries > 0; --retries) {
                try {
                    files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    break;
                } catch (IOException) {
                }
                Thread.Sleep(1000);
            }

            while (confirmation == null || files.Except(confirmation).Any()) {
                Thread.Sleep(500);
                confirmation = files;
                files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            }

            return files;
        }

        private static string[] PublishAndWaitForFiles(VisualStudioApp app, string command, string dir) {
            app.Dte.ExecuteCommand(command);

            return WaitForFiles(dir);
        }

        public void TestPublishFiles(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                app.OnDispose(() => project.Properties.Item("PublishUrl").Value = "");
                string dir = Path.Combine(TestSharePublic, subDir);

                app.OpenSolutionExplorer().SelectProject(project);

                var files = PublishAndWaitForFiles(app, "Build.PublishSelection", dir);

                Assert.IsNotNull(files, "Timed out waiting for files to publish");
                Assert.AreEqual(1, files.Length);
                Assert.AreEqual("Program.py", Path.GetFileName(files[0]));

                Directory.Delete(dir, true);
            } finally {
                WNetCancelConnection2(TestSharePublic, 0, true);
            }
        }

        public void TestPublishReadOnlyFiles(VisualStudioApp app) {
            var sourceFile = TestData.GetPath(@"TestData\HelloWorld\Program.py");
            Assert.IsTrue(File.Exists(sourceFile), sourceFile + " not found");
            var attributes = File.GetAttributes(sourceFile);

            var project = app.OpenProject(@"TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                app.OnDispose(() => project.Properties.Item("PublishUrl").Value = "");
                string dir = Path.Combine(TestSharePublic, subDir);

                File.SetAttributes(sourceFile, attributes | FileAttributes.ReadOnly);

                app.OpenSolutionExplorer().SelectProject(project);

                var files = PublishAndWaitForFiles(app, "Build.PublishSelection", dir);

                Assert.IsNotNull(files, "Timed out waiting for files to publish");
                Assert.AreEqual(1, files.Length);
                Assert.AreEqual("Program.py", Path.GetFileName(files[0]));
                Assert.IsTrue(File.GetAttributes(sourceFile).HasFlag(FileAttributes.ReadOnly), "Source file should be read-only");
                Assert.IsFalse(File.GetAttributes(files[0]).HasFlag(FileAttributes.ReadOnly), "Published file should not be read-only");

                Directory.Delete(dir, true);
            } finally {
                WNetCancelConnection2(TestSharePublic, 0, true);
                File.SetAttributes(sourceFile, attributes);
            }
        }

        public void TestPublishVirtualEnvironment(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\VirtualEnv.sln"));
            var dir = TestData.GetTempPath();
            project.Properties.Item("PublishUrl").Value = dir;
            app.OnDispose(() => project.Properties.Item("PublishUrl").Value = "");

            app.OpenSolutionExplorer().SelectProject(project);
            var files = PublishAndWaitForFiles(app, "Build.PublishSelection", dir);

            Assert.IsNotNull(files, "Timed out waiting for files to publish");
            AssertUtil.ContainsAtLeast(
                files.Select(f => CommonUtils.GetRelativeFilePath(dir, f).ToLowerInvariant()),
                "env\\include\\pyconfig.h",
                "env\\lib\\site.py",
                "env\\scripts\\python.exe",
                "program.py"
            );

            Directory.Delete(dir, true);
        }
    }
}

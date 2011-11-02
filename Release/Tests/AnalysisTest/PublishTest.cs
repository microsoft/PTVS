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
using System.IO;
using System.Linq;
using System.Threading;
using AnalysisTest.ProjectSystem;
using AnalysisTest.UI;
using Microsoft.PythonTools;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class PublishTest {
        private const string TestFtpUrl = "ftp://anonymous:blazzz@dinov1/testdir";
        private const string FtpValidateDir = "\\\\dinov1\\ftproot\\testdir";
        private const string TestSharePublic = "\\\\dinov1\\Test";
        private const string TestSharePrivate = "\\\\dinov1\\PubTest";
        private const string PrivateShareUser = "dinov1\\TestUser";
        private const string PrivateShareUserWithoutMachine = "TestUser";
        private const string PrivateSharePassword = "!10ctopus";
        private const string PrivateSharePasswordIncorrect = "NotThisPassword";

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }


        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFiles() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                string dir = Path.Combine(TestSharePublic, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                System.Threading.Thread.Sleep(2000);
                var files = Directory.GetFiles(dir);
                Assert.AreEqual(files.Length, 1);
                Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                Directory.Delete(dir, true);
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesControlled() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\PublishTest.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                string dir = Path.Combine(TestSharePublic, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'PublishTest' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                System.Threading.Thread.Sleep(2000);
                var files = Directory.GetFiles(dir);
                Assert.AreEqual(files.Length, 2);
                files = files.Select(x => Path.GetFileName(x)).ToArray();
                Assert.IsTrue(files.Contains("Program.py"));
                Assert.IsTrue(files.Contains("TextFile.txt"));

                Directory.Delete(dir, true);
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonate() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                string dir = Path.Combine(TestSharePrivate, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));

                var creds = new CredentialsDialog(app.WaitForDialog());
                creds.UserName = PrivateShareUser;
                creds.Password = PrivateSharePassword;
                creds.Ok();

                System.Threading.Thread.Sleep(2000);

                using (var impHelper = new ImpersonationHelper(new System.Net.NetworkCredential(PrivateShareUser.Split('\\')[1], PrivateSharePassword, PrivateShareUser.Split('\\')[0]))) {
                    for (int i = 0; i < 10 && !Directory.Exists(dir); i++) {
                        System.Threading.Thread.Sleep(1000);
                    }

                    var files = Directory.GetFiles(dir);
                    Assert.AreEqual(files.Length, 1);
                    Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                    Directory.Delete(dir, true);
                }
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateNoMachineName() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                string dir = Path.Combine(TestSharePrivate, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));

                var creds = new CredentialsDialog(app.WaitForDialog());
                creds.UserName = PrivateShareUserWithoutMachine;
                creds.Password = PrivateSharePassword;
                creds.Ok();

                System.Threading.Thread.Sleep(2000);

                using (var impHelper = new ImpersonationHelper(new System.Net.NetworkCredential(PrivateShareUser.Split('\\')[1], PrivateSharePassword, PrivateShareUser.Split('\\')[0]))) {
                    var files = Directory.GetFiles(dir);
                    Assert.AreEqual(files.Length, 1);
                    Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                    Directory.Delete(dir, true);
                }
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateWrongCredentials() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                string dir = Path.Combine(TestSharePrivate, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));

                var creds = new CredentialsDialog(app.WaitForDialog());
                creds.UserName = PrivateShareUser;
                creds.Password = PrivateSharePasswordIncorrect;
                creds.Ok();

                System.Threading.Thread.Sleep(2000);

                var statusBar = (IVsStatusbar)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsStatusbar));
                string text;
                ErrorHandler.ThrowOnFailure(statusBar.GetText(out text));

                const string expected = "Publish failed: Incorrect user name or password: ";
                Assert.IsTrue(text.StartsWith(expected), "Expected '{0}', got '{1}'", expected, text);
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateCancelCredentials() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                string dir = Path.Combine(TestSharePrivate, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));

                var creds = new CredentialsDialog(app.WaitForDialog());
                creds.UserName = PrivateShareUser;
                creds.Password = PrivateSharePasswordIncorrect;
                creds.Cancel();

                System.Threading.Thread.Sleep(2000);

                var statusBar = (IVsStatusbar)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsStatusbar));
                string text;
                ErrorHandler.ThrowOnFailure(statusBar.GetText(out text));

                const string expected = "Publish failed: Access to the path '";
                Assert.IsTrue(text.StartsWith(expected), "Expected '{0}', got '{1}'", expected, text);
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFtp() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            try {
                string subDir = Guid.NewGuid().ToString();
                string url = TestFtpUrl + "/" + subDir;
                project.Properties.Item("PublishUrl").Value = url;
                string dir = Path.Combine(FtpValidateDir, subDir);
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find Program.py, send copy & paste, verify copy of file is there
                var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                programPy.SetFocus();

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                System.Threading.Thread.Sleep(2000);
                var files = Directory.GetFiles(dir);
                Assert.AreEqual(files.Length, 1);
                Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                // do it again w/ the directories already existing
                File.Delete(files[0]);
                programPy.SetFocus();
                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                System.Threading.Thread.Sleep(2000);
                files = Directory.GetFiles(dir);
                Assert.AreEqual(files.Length, 1);
                Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                Directory.Delete(dir, true);
            } finally {
                project.Properties.Item("PublishUrl").Value = "";
            }
        }
    }
}

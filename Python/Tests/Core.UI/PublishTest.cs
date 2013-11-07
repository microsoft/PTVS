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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class PublishTest {
        private static string TestFtpUrl = "ftp://anonymous:blazzz@" + GetPyToolsIp() + "/testdir";

        private const string FtpValidateDir = "\\\\pytools\\ftproot$\\testdir";
        private const string TestSharePublic = "\\\\pytools\\Test$";
        private const string TestSharePrivate = "\\\\pytools\\PubTest$";
        private const string PrivateShareUser = "pytools\\TestUser";
        private const string PrivateShareUserWithoutMachine = "TestUser";
        private const string PrivateSharePassword = "!10ctopus";
        private const string PrivateSharePasswordIncorrect = "NotThisPassword";

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        public TestContext TestContext { get; set; }

        [DllImport("mpr")]
        static extern uint WNetCancelConnection2(string lpName, uint dwFlags, bool fForce);

        private static string GetPyToolsIp() {
            // try ipv4
            foreach (var entry in Dns.GetHostEntry("pytools").AddressList) {
                if (entry.AddressFamily == AddressFamily.InterNetwork) {
                    return entry.ToString();
                }
            }

            // fallback to anything
            foreach (var entry in Dns.GetHostEntry("pytools").AddressList) {
                return entry.ToString();
            }

            throw new InvalidOperationException();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFiles() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                try {
                    string subDir = Guid.NewGuid().ToString();
                    project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                    string dir = Path.Combine(TestSharePublic, subDir);

                    app.OpenSolutionExplorer();
                    var window = app.SolutionExplorerTreeView;

                    // find Program.py, send copy & paste, verify copy of file is there
                    var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                    AutomationWrapper.Select(programPy);

                    ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                    System.Threading.Thread.Sleep(2000);
                    var files = Directory.GetFiles(dir);
                    Assert.AreEqual(files.Length, 1);
                    Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                    Directory.Delete(dir, true);
                } finally {
                    project.Properties.Item("PublishUrl").Value = "";
                    WNetCancelConnection2(TestSharePublic, 0, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesControlled() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\PublishTest.sln");
                try {
                    string subDir = Guid.NewGuid().ToString();
                    project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePublic, subDir);
                    string dir = Path.Combine(TestSharePublic, subDir);

                    app.OpenSolutionExplorer();
                    var window = app.SolutionExplorerTreeView;

                    // find Program.py, send copy & paste, verify copy of file is there
                    var programPy = window.FindItem("Solution 'PublishTest' (1 project)", "HelloWorld");

                    AutomationWrapper.Select(programPy);

                    using (var publishComplete = new AutoResetEvent(false))
                    using (var tcs = new CancellationTokenSource()) {
                        const double TIMEOUT = 10; // seconds
                        tcs.CancelAfter(TimeSpan.FromSeconds(TIMEOUT));

                        ThreadPool.QueueUserWorkItem(x => {
                            try {
                                app.Dte.ExecuteCommand("Build.PublishSelection");

                                while (!tcs.IsCancellationRequested) {
                                    try {
                                        Directory.GetFiles(dir);
                                        break;
                                    } catch (IOException) {
                                    } catch (UnauthorizedAccessException) {
                                    }
                                    Thread.Sleep(1000);
                                }
                            } finally {
                                publishComplete.Set();
                            }
                        });
                        Assert.IsTrue(publishComplete.WaitOne(TimeSpan.FromSeconds(TIMEOUT)), "Publish timed out");
                    }

                    var files = Directory.GetFiles(dir);
                    Assert.AreEqual(files.Length, 2);
                    files = files.Select(x => Path.GetFileName(x)).ToArray();
                    Assert.IsTrue(files.Contains("Program.py"));
                    Assert.IsTrue(files.Contains("TextFile.txt"));

                    Directory.Delete(dir, true);
                } finally {
                    project.Properties.Item("PublishUrl").Value = "";
                    app.DismissAllDialogs();
                    VsIdeTestHostContext.Dte.Solution.Close(false);
                    WNetCancelConnection2(TestSharePrivate, 0, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonate() {
            WNetCancelConnection2(TestSharePrivate, 0, true);
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");
                    try {
                        string subDir = Guid.NewGuid().ToString();
                        project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);

                        app.OpenSolutionExplorer();
                        var window = app.SolutionExplorerTreeView;

                        // find Program.py, send copy & paste, verify copy of file is there
                        var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                        AutomationWrapper.Select(programPy);

                        var creds = new CredentialsDialog(app.OpenDialogWithDteExecuteCommand("Build.PublishSelection"));
                        creds.UserName = PrivateShareUserWithoutMachine;
                        creds.Password = PrivateSharePassword;
                        creds.Ok();

                        System.Threading.Thread.Sleep(2000);

                        string dir = Path.Combine(TestSharePrivate, subDir);

                        for (int i = 0; i < 10 && !Directory.Exists(dir); i++) {
                            System.Threading.Thread.Sleep(1000);
                        }

                        var files = Directory.GetFiles(dir);
                        Assert.AreEqual(files.Length, 1);
                        Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                        Directory.Delete(dir, true);
                    } finally {
                        project.Properties.Item("PublishUrl").Value = "";
                    }
                } finally {
                    WNetCancelConnection2(TestSharePrivate, 0, true);
                }
            }
        }

        class NetUseHelper : IDisposable {
            public readonly string Drive;   // drive, with colon, without backslash

            public NetUseHelper() {
                var procInfo = new ProcessStartInfo(
                    Path.Combine(Environment.SystemDirectory, "net.exe"),
                    String.Format("use * {0} /user:{1} {2}",
                        TestSharePrivate,
                        PrivateShareUser,
                        PrivateSharePassword
                    )
                );
                procInfo.RedirectStandardOutput = true;
                procInfo.RedirectStandardError = true;
                procInfo.UseShellExecute = false;
                procInfo.CreateNoWindow = true;
                var process = Process.Start(procInfo);
                var line = process.StandardOutput.ReadToEnd();
                if (!line.StartsWith("Drive ")) {
                    throw new InvalidOperationException("didn't get expected drive output " + line);
                }
                Drive = line.Substring(6, 2);
                process.Close();
            }

            public void Dispose() {
                var procInfo = new ProcessStartInfo(
                    Path.Combine(Environment.SystemDirectory, "net.exe"),
                    "use /delete " + Drive
                );
                procInfo.RedirectStandardOutput = true;
                procInfo.UseShellExecute = false;
                procInfo.CreateNoWindow = true;
                var process = Process.Start(procInfo);
                process.WaitForExit();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateNoMachineName() {
            WNetCancelConnection2(TestSharePrivate, 0, true);
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");
                    try {
                        string subDir = Guid.NewGuid().ToString();
                        project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);

                        app.OpenSolutionExplorer();
                        var window = app.SolutionExplorerTreeView;

                        // find Program.py, send copy & paste, verify copy of file is there
                        var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                        AutomationWrapper.Select(programPy);

                        var creds = new CredentialsDialog(app.OpenDialogWithDteExecuteCommand("Build.PublishSelection"));
                        creds.UserName = PrivateShareUserWithoutMachine;
                        creds.Password = PrivateSharePassword;
                        creds.Ok();

                        System.Threading.Thread.Sleep(2000);

                        using (var helper = new NetUseHelper()) {
                            string dir = Path.Combine(helper.Drive + "\\", subDir);
                            var files = Directory.GetFiles(dir);
                            Assert.AreEqual(files.Length, 1);
                            Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                            Directory.Delete(dir, true);
                        }
                    } finally {
                        project.Properties.Item("PublishUrl").Value = "";
                    }
                } finally {
                    WNetCancelConnection2(TestSharePrivate, 0, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateWrongCredentials() {
            WNetCancelConnection2(TestSharePrivate, 0, true);
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");
                    try {
                        string subDir = Guid.NewGuid().ToString();
                        project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                        string dir = Path.Combine(TestSharePrivate, subDir);

                        app.OpenSolutionExplorer();
                        var window = app.SolutionExplorerTreeView;

                        // find Program.py, send copy & paste, verify copy of file is there
                        var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                        AutomationWrapper.Select(programPy);

                        var creds = new CredentialsDialog(app.OpenDialogWithDteExecuteCommand("Build.PublishSelection"));
                        creds.UserName = PrivateShareUser;
                        creds.Password = PrivateSharePasswordIncorrect;
                        creds.Ok();

                        const string expected = "Publish failed: Incorrect user name or password: ";

                        string text = "";
                        for (int i = 0; i < 5; i++) {
                            var statusBar = app.GetService<IVsStatusbar>(typeof(SVsStatusbar));
                            ErrorHandler.ThrowOnFailure(statusBar.GetText(out text));
                            if (text.StartsWith(expected)) {
                                break;
                            }
                            System.Threading.Thread.Sleep(2000);
                        }

                        Assert.IsTrue(text.StartsWith(expected), "Expected '{0}', got '{1}'", expected, text);
                    } finally {
                        project.Properties.Item("PublishUrl").Value = "";
                    }
                } finally {
                    WNetCancelConnection2(TestSharePrivate, 0, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFilesImpersonateCancelCredentials() {
            WNetCancelConnection2(TestSharePrivate, 0, true);
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");
                    try {
                        string subDir = Guid.NewGuid().ToString();
                        project.Properties.Item("PublishUrl").Value = Path.Combine(TestSharePrivate, subDir);
                        string dir = Path.Combine(TestSharePrivate, subDir);

                        app.OpenSolutionExplorer();
                        var window = app.SolutionExplorerTreeView;

                        // find Program.py, send copy & paste, verify copy of file is there
                        var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                        AutomationWrapper.Select(programPy);

                        var creds = new CredentialsDialog(app.OpenDialogWithDteExecuteCommand("Build.PublishSelection"));
                        creds.UserName = PrivateShareUser;
                        creds.Password = PrivateSharePasswordIncorrect;
                        creds.Cancel();

                        var statusBar = app.GetService<IVsStatusbar>(typeof(SVsStatusbar));
                        string text = null;
                        const string expected = "Publish failed: Access to the path";

                        for (int i = 0; i < 10; i++) {
                            ErrorHandler.ThrowOnFailure(statusBar.GetText(out text));

                            if (text.StartsWith(expected)) {
                                break;
                            }
                            System.Threading.Thread.Sleep(1000);
                        }

                        Assert.IsTrue(text.StartsWith(expected), "Expected '{0}', got '{1}'", expected, text);
                    } finally {
                        project.Properties.Item("PublishUrl").Value = "";
                    }
                } finally {
                    WNetCancelConnection2(TestSharePrivate, 0, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPublishFtp() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                try {
                    string subDir = Guid.NewGuid().ToString();
                    string url = TestFtpUrl + "/" + subDir;
                    project.Properties.Item("PublishUrl").Value = url;
                    string dir = Path.Combine(FtpValidateDir, subDir);
                    Debug.WriteLine(dir);

                    app.OpenSolutionExplorer();
                    var window = app.SolutionExplorerTreeView;

                    // find Program.py, send copy & paste, verify copy of file is there
                    var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");

                    AutomationWrapper.Select(programPy);

                    ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                    System.Threading.Thread.Sleep(2000);
                    var files = WaitForFiles(dir);
                    Assert.AreEqual(files.Length, 1);
                    Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                    // do it again w/ the directories already existing
                    File.Delete(files[0]);
                    AutomationWrapper.Select(programPy);
                    ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Build.PublishSelection"));
                    files = WaitForFiles(dir);
                    Assert.AreEqual(files.Length, 1);
                    Assert.AreEqual(Path.GetFileName(files[0]), "Program.py");

                    Directory.Delete(dir, true);
                } finally {
                    project.Properties.Item("PublishUrl").Value = "";
                    app.DismissAllDialogs();
                    VsIdeTestHostContext.Dte.Solution.Close(false);
                }
            }
        }

        private static string[] WaitForFiles(string dir) {
            string[] files = null;
            for (int i = 0; i < 10; i++) {
                try {
                    files = Directory.GetFiles(dir);
                } catch {
                }
                if (files == null || files.Length != 0) {
                    break;
                }
                System.Threading.Thread.Sleep(3000);
            }
            return files;
        }
    }
}

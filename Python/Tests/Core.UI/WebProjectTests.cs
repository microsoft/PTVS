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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using InterpreterExt = analysis::Microsoft.PythonTools.Interpreter.PythonInterpreterFactoryExtensions;

namespace PythonToolsUITests {
    [TestClass]
    public class WebProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadWebFlavoredProject() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\EmptyWebProject.sln");
                Assert.AreEqual("EmptyWebProject.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

                var catids = VsIdeTestHostContext.Dte.ObjectExtenders.GetContextualExtenderCATIDs();
                dynamic extender = project.Extender["WebApplication"];
                Assert.IsNotNull(extender, "No WebApplication extender");
                extender.StartWebServerOnDebug = true;
                extender.StartWebServerOnDebug = false;

                var proj = project.GetCommonProject();
                var ccp = proj as IPythonProject2;
                Assert.IsNotNull(ccp);
                Assert.IsNotNull(ccp.FindCommand("PythonRunWebServerCommand"), "No PythonRunWebServerCommand");
                Assert.IsNotNull(ccp.FindCommand("PythonDebugWebServerCommand"), "No PythonDebugWebServerCommand");
                Assert.IsNull(ccp.FindCommand("PythonUpgradeWebFrameworkCommand"), "Unexpected PythonUpgradeWebFrameworkCommand");
            }
        }

        #region EndToEndTest

        private static void EndToEndTest(
            string templateName,
            string moduleName,
            string textInResponse,
            string pythonVersion
        ) {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var newProjDialog = app.FileNewProject();

                newProjDialog.FocusLanguageNode();
                newProjDialog.Location = TestData.GetTempPath();
                newProjDialog.ProjectTypes.FindItem(templateName).Select();
                newProjDialog.ClickOK();

                for (int i = 0; i < 10 && app.Dte.Solution.Projects.Count == 0; i++) {
                    System.Threading.Thread.Sleep(1000);
                }
                Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

                var pyProj = app.Dte.Solution.Projects.Item(1).GetPythonProject();
                Assert.IsInstanceOfType(pyProj.GetLauncher(), typeof(PythonWebLauncher));

                var model = app.GetService<IComponentModel>(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();
                var task = pyProj.CreateOrAddVirtualEnvironment(
                    service,
                    true,
                    Path.Combine(pyProj.ProjectHome, "env"),
                    service.FindInterpreter(PythonPaths.CPythonGuid, pythonVersion),
                    pythonVersion == "3.3"
                );
                task.Wait();
                var factory = task.Result;
                Assert.IsTrue(factory.Id == pyProj.GetInterpreterFactory().Id);

                // Ensure pip is installed so we don't have to click through the
                // dialog.
                Pip.InstallPip(factory, false).Wait();

                var cmd = ((IPythonProject2)pyProj).FindCommand("PythonUpgradeWebFrameworkCommand");
                cmd.Execute(null);

                Assert.AreEqual(1, InterpreterExt.FindModules(factory, moduleName).Count);

                // Wait for analysis to complete so we don't have too many
                // python.exe processes floating around.
                for (int i = 0; i < 60 && Process.GetProcessesByName("Microsoft.PythonTools.Analyzer").Any(); --i) {
                    Thread.Sleep(1000);
                }

                UIThread.Instance.RunSync(() => {
                    pyProj.SetProjectProperty("WebServerPort", "23457");
                });
                LaunchAndVerifyNoDebug(pyProj, 23457, textInResponse);

                UIThread.Instance.RunSync(() => {
                    pyProj.SetProjectProperty("WebServerPort", "23456");
                });
                LaunchAndVerifyDebug(app, 23456, textInResponse);
            }
        }

        private static void LaunchAndVerifyDebug(VisualStudioApp app, int port, string textInResponse) {
            app.Dte.Debugger.Go(false);
            
            string text = string.Empty;
            try {
                for (int i = 100;
                    i > 0 &&
                        (app.Dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgRunMode ||
                        !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port));
                    --i) {
                    Thread.Sleep(300);
                }

                var req = HttpWebRequest.CreateHttp(new Uri(string.Format("http://localhost:{0}/", port)));
                using (var resp = req.GetResponse()) {
                    text = new StreamReader(resp.GetResponseStream()).ReadToEnd();
                }
            } finally {
                app.Dte.Debugger.Stop();
            }

            Console.WriteLine("Response from http://localhost:{0}/", port);
            Console.WriteLine(text);
            Assert.IsTrue(text.Contains(textInResponse), text);

            for (int i = 10;
                i > 0 &&
                    (app.Dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgRunMode ||
                    !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().All(p => p.Port != port));
                --i) {
                Thread.Sleep(500);
            }
        }

        private static void LaunchAndVerifyNoDebug(PythonProjectNode project, int port, string textInResponse) {
            var pythonProcesses = Process.GetProcessesByName("python");

            UIThread.Instance.RunSync(() => {
                var prevNormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit;
                var prevAbnormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit;
                try {
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = false;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = false;

                    ErrorHandler.ThrowOnFailure(project.GetLauncher().LaunchProject(false));
                } finally {
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = prevNormal;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = prevAbnormal;
                }
            });

            for (int i = 100;
                i > 0 &&
                    !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port);
                --i) {
                Thread.Sleep(300);
            }

            Process newProcess = null;
            for (int i = 20;
                i > 0 && newProcess == null;
                --i, newProcess = Process.GetProcessesByName("python").Except(pythonProcesses).FirstOrDefault()) {
                Thread.Sleep(500);
            }
            Assert.IsNotNull(newProcess, "Did not find new Python process");

            string text;
            var req = HttpWebRequest.CreateHttp(new Uri(string.Format("http://localhost:{0}/", port)));
            using (var resp = req.GetResponse()) {
                text = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            }

            for (int i = 10; i >= 0; --i) {
                try {
                    newProcess.Kill();
                    break;
                } catch {
                    Thread.Sleep(100);
                }
                if (i == 0) {
                    Console.WriteLine("Failed to kill process.");
                }
            }

            Console.WriteLine("Response from http://localhost:{0}/", port);
            Console.WriteLine(text);
            Assert.IsTrue(text.Contains(textInResponse), text);
        }

        #endregion

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FlaskEndToEndV33() {
            EndToEndTest("Flask Web Project", "flask", "Hello World!", "3.3");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FlaskEndToEndV27() {
            EndToEndTest("Flask Web Project", "flask", "Hello World!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BottleEndToEndV33() {
            EndToEndTest("Bottle Web Project", "bottle", "Hello World!", "3.3");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BottleEndToEndV27() {
            EndToEndTest("Bottle Web Project", "bottle", "Hello World!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DjangoEndToEndV27() {
            EndToEndTest("Django Web Project", "django", "Congratulations on your first Django-powered page.", "2.7");
        }
    }
}

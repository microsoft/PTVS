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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using InterpreterExt = analysis::Microsoft.PythonTools.Interpreter.PythonInterpreterFactoryExtensions;

namespace PythonToolsUITests {
    [TestClass]
    public class WebProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        public TestContext TestContext { get; set; }

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

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void WebProjectCommandLineArgs() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\CheckCommandLineArgs.sln");

                var proj = project.GetCommonProject() as IPythonProject2;
                Assert.IsNotNull(proj);

                var outFile = Path.Combine(Path.GetDirectoryName(project.FullName), "output.txt");

                foreach (var cmdName in new[] { "PythonRunWebServerCommand", "PythonDebugWebServerCommand" }) {
                    Console.WriteLine("Testing {0}, writing to {1}", cmdName, outFile);

                    if (File.Exists(outFile)) {
                        File.Delete(outFile);
                    }

                    var outData = Guid.NewGuid().ToString("N");

                    ThreadHelper.Generic.Invoke(() => {
                        proj.SetProperty("CommandLineArguments", outData + " \"" + outFile + "\"");
                        proj.FindCommand(cmdName).Execute(proj);
                    });

                    for (int retries = 10; retries > 0 && !File.Exists(outFile); --retries) {
                        Thread.Sleep(100);
                    }

                    Assert.AreEqual(outData, File.ReadAllText(outFile).Trim());
                }
            }
        }

        #region EndToEndTest

        private void EndToEndTest(
            string templateName,
            string moduleName,
            string textInResponse,
            string pythonVersion
        ) {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var pyProj = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    templateName,
                    TestData.GetTempPath(),
                    TestContext.TestName
                ).GetPythonProject();

                Assert.IsInstanceOfType(pyProj.GetLauncher(), typeof(PythonWebLauncher));

                var factory = CreateVirtualEnvironment(pythonVersion, app, pyProj);

                InstallWebFramework(moduleName, pyProj, factory);

                // Wait for analysis to complete so we don't have too many
                // python.exe processes floating around.
                for (int i = 0; i < 60 && Process.GetProcessesByName("Microsoft.PythonTools.Analyzer").Any(); --i) {
                    Thread.Sleep(1000);
                }

                UIThread.Instance.RunSync(() => {
                    pyProj.SetProjectProperty("WebBrowserPort", "23457");
                });
                LaunchAndVerifyNoDebug(app, 23457, textInResponse);

                UIThread.Instance.RunSync(() => {
                    pyProj.SetProjectProperty("WebBrowserPort", "23456");
                });
                LaunchAndVerifyDebug(app, 23456, textInResponse);
            }
        }


        private static void LaunchAndVerifyDebug(VisualStudioApp app, int port, string textInResponse) {
            app.Dte.Solution.SolutionBuild.Build(true);
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

                text = WebDownloadUtility.GetString(new Uri(string.Format("http://localhost:{0}/", port)));
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

        private static void LaunchAndVerifyNoDebug(
            VisualStudioApp app,
            int port,
            string textInResponse
        ) {
            var pythonProcesses = Process.GetProcessesByName("python");
            Process[] newProcesses;
            bool prevNormal = true, prevAbnormal = true;

            try {
                UIThread.Instance.RunSync(() => {
                    app.Dte.Solution.SolutionBuild.Build(true);
                    prevNormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit;
                    prevAbnormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = false;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = false;

                    app.Dte.Solution.SolutionBuild.Run();
                });

                newProcesses = new Process[0];
                for (int i = 20;
                    i > 0 && !newProcesses.Any();
                    --i, newProcesses = Process.GetProcessesByName("python").Except(pythonProcesses).ToArray()) {
                    Thread.Sleep(500);
                }
                Assert.IsTrue(newProcesses.Any(), "Did not find new Python process");

                for (int i = 100;
                    i > 0 &&
                        !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port);
                    --i) {
                    Thread.Sleep(300);
                }
            } finally {
                UIThread.Instance.RunSync(() => {
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = prevNormal;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = prevAbnormal;
                });
            }
            string text;
            var req = HttpWebRequest.CreateHttp(new Uri(string.Format("http://localhost:{0}/", port)));
            using (var resp = req.GetResponse()) {
                text = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            }

            for (int i = 10; i >= 0; --i) {
                bool allKilled = true;
                for (int j = 0; j < newProcesses.Length; ++j) {
                    try {
                        if (newProcesses[j] != null) {
                            if (!newProcesses[j].HasExited) {
                                newProcesses[j].Kill();
                            }
                            newProcesses[j] = null;
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Failed to kill {0}", newProcesses[j]);
                        Console.WriteLine(ex);
                        Console.WriteLine();
                        allKilled = false;
                    }
                }
                if (allKilled) {
                    break;
                }
                Thread.Sleep(100);
                Assert.AreNotEqual(0, i, "Failed to kill process.");
            }

            Console.WriteLine("Response from http://localhost:{0}/", port);
            Console.WriteLine(text);
            Assert.IsTrue(text.Contains(textInResponse), text);

            for (int i = 10;
                i > 0 && !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().All(p => p.Port != port);
                --i) {
                Thread.Sleep(500);
            }
        }

        internal static IPythonInterpreterFactory CreateVirtualEnvironment(string pythonVersion, VisualStudioApp app, PythonProjectNode pyProj) {
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
            return factory;
        }

        internal static void InstallWebFramework(string moduleName, PythonProjectNode pyProj, IPythonInterpreterFactory factory) {
            // Ensure pip is installed so we don't have to click through the
            // dialog.
            Pip.InstallPip(factory, false).Wait();

            UIThread.Instance.RunSync(() => {
                var cmd = ((IPythonProject2)pyProj).FindCommand("PythonUpgradeWebFrameworkCommand");
                cmd.Execute(null);
            });

            Assert.AreEqual(1, InterpreterExt.FindModules(factory, moduleName).Count);
        }

        #endregion

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FlaskEndToEndV33() {
            EndToEndTest(PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "3.3");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FlaskEndToEndV27() {
            EndToEndTest(PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BottleEndToEndV33() {
            EndToEndTest(PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "3.3");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BottleEndToEndV27() {
            EndToEndTest(PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DjangoEndToEndV27() {
            EndToEndTest(
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "2.7"
            );
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DjangoEndToEndV33() {
            EndToEndTest(
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "3.3"
            );
        }
    }
}

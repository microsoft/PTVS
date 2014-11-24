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
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Xml;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using InterpreterExt = analysis::Microsoft.PythonTools.Interpreter.PythonInterpreterFactoryExtensions;
using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;

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
        [HostType("VSTestHost")]
        public void LoadWebFlavoredProject() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\EmptyWebProject.sln");
                Assert.AreEqual("EmptyWebProject.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

                var catids = app.Dte.ObjectExtenders.GetContextualExtenderCATIDs();
                dynamic extender = project.Extender["WebApplication"];
                Assert.IsNotNull(extender, "No WebApplication extender");
                extender.StartWebServerOnDebug = true;
                extender.StartWebServerOnDebug = false;

                var proj = project.GetCommonProject();
                var ccp = proj as IPythonProject2;
                Assert.IsNotNull(ccp);
                Assert.IsNotNull(ccp.FindCommand("PythonRunWebServerCommand"), "No PythonRunWebServerCommand");
                Assert.IsNotNull(ccp.FindCommand("PythonDebugWebServerCommand"), "No PythonDebugWebServerCommand");
            }
        }

        private static void CheckCommandLineArgs(string setValue, string expectedValue = null) {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\CheckCommandLineArgs.sln");

                var proj = project.GetCommonProject() as IPythonProject2;
                Assert.IsNotNull(proj);

                var outFile = Path.Combine(Path.GetDirectoryName(project.FullName), "output.txt");

                foreach (var cmdName in new[] { "PythonRunWebServerCommand", "PythonDebugWebServerCommand" }) {
                    Console.WriteLine("Testing {0}, writing to {1}", cmdName, outFile);

                    if (File.Exists(outFile)) {
                        File.Delete(outFile);
                    }

                    UIThread.Invoke(() => {
                        proj.SetProperty("CommandLineArguments", string.Format("\"{0}\" \"{1}\"", setValue, outFile));
                        proj.FindCommand(cmdName).Execute(proj);
                    });

                    for (int retries = 10; retries > 0 && !File.Exists(outFile); --retries) {
                        Thread.Sleep(100);
                    }

                    Assert.AreEqual(expectedValue ?? setValue, File.ReadAllText(outFile).Trim());
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectCommandLineArgs() {
            CheckCommandLineArgs(Guid.NewGuid().ToString("N"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectStartupModuleArgs() {
            CheckCommandLineArgs("{StartupModule}", "CheckCommandLineArgs");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectStaticUri() {
            using (var app = new VisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.EmptyWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectStaticUri"
                );

                var proj = project.GetCommonProject();
                Assert.IsNotNull(proj);

                UIThread.Invoke(() => {
                    proj.SetProjectProperty("PythonWsgiHandler", "NoHandler");

                    proj.SetProjectProperty("StaticUriPattern", "");
                    proj.SetProjectProperty("StaticUriRewrite", "");
                });
                app.ExecuteCommand("Build.RebuildSolution");
                app.WaitForOutputWindowText("Build", "1 succeeded");

                var webConfig = File.ReadAllText(Path.Combine(Path.GetDirectoryName(project.FullName), "web.config"));
                if (!webConfig.Contains(@"<add input=""true"" pattern=""false"" />")) {
                    Assert.Fail(string.Format("Did not find Static Files condition in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }

                UIThread.Invoke(() => {
                    proj.SetProjectProperty("StaticUriPattern", "^static/.*$");
                });
                app.ExecuteCommand("Build.RebuildSolution");
                app.WaitForOutputWindowText("Build", "1 succeeded");

                webConfig = File.ReadAllText(Path.Combine(Path.GetDirectoryName(project.FullName), "web.config"));
                if (!webConfig.Contains(@"<add input=""{REQUEST_URI}"" pattern=""^static/.*$"" ignoreCase=""true"" negate=""true"" />")) {
                    Assert.Fail(string.Format("Did not find rewrite condition in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }
                if (!webConfig.Contains(@"<add input=""true"" pattern=""false"" />")) {
                    Assert.Fail(string.Format("Did not find Static Files condition in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }

                UIThread.Invoke(() => {
                    proj.SetProjectProperty("StaticUriRewrite", "static_files/{R:1}");
                });
                app.ExecuteCommand("Build.RebuildSolution");
                app.WaitForOutputWindowText("Build", "1 succeeded");

                webConfig = File.ReadAllText(Path.Combine(Path.GetDirectoryName(project.FullName), "web.config"));
                if (webConfig.Contains(@"<add input=""{REQUEST_URI}"" pattern=""^static/.*$"" ignoreCase=""true"" negate=""true"" />")) {
                    Assert.Fail(string.Format("Found old rewrite condition in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }
                if (!webConfig.Contains(@"<action type=""Rewrite"" url=""static_files/{R:1}"" appendQueryString=""true"" />")) {
                    Assert.Fail(string.Format("Did not find rewrite action in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }
                if (webConfig.Contains(@"<add input=""true"" pattern=""false"" />")) {
                    Assert.Fail(string.Format("Should not have found Static Files condition in:{0}{0}{1}",
                        Environment.NewLine,
                        webConfig
                    ));
                }

                UIThread.Invoke(() => {
                    proj.SetProjectProperty("StaticUriPattern", "invalid[pattern");
                });
                app.ExecuteCommand("Build.RebuildSolution");
                app.WaitForOutputWindowText("Build", "1 failed");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectBuildWarnings() {
            using (var app = new PythonVisualStudioApp())
            using (app.SelectDefaultInterpreter(PythonPaths.Python33 ?? PythonPaths.Python33_x64)) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.EmptyWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectBuildWarnings"
                );

                var proj = project.GetCommonProject();
                Assert.IsNotNull(proj);

                for (int iteration = 0; iteration <= 2; ++iteration) {
                    var warnings = UIThread.Invoke(() => {
                        var buildPane = app.GetOutputWindow("Build");
                        buildPane.Clear();

                        project.DTE.Solution.SolutionBuild.Clean(true);
                        project.DTE.Solution.SolutionBuild.Build(true);

                        var text = app.GetOutputWindowText("Build");
                        return text.Split('\r', '\n')
                            .Select(s => Regex.Match(s, @"warning\s*:\s*(?<msg>.+)"))
                            .Where(m => m.Success)
                            .Select(m => m.Groups["msg"].Value)
                            .ToList();
                    });

                    Console.WriteLine("Warnings:{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, warnings));
                    if (iteration < 2) {
                        Assert.IsNotNull(
                            warnings.FirstOrDefault(s => Regex.IsMatch(s, @"Python( 64-bit)? 3\.3 is not natively supported.+")),
                            "Missing \"not natively supported\" warning"
                        );
                    } else {
                        // Third time through, we've fixed this warning.
                        Assert.IsNull(
                            warnings.FirstOrDefault(s => Regex.IsMatch(s, @"Python( 64-bit)? 3\.3 is not natively supported.+")),
                            "Still recieved \"not natively supported\" warning"
                        );
                    }

                    if (iteration < 1) {
                        Assert.IsNotNull(
                            warnings.FirstOrDefault(s => Regex.IsMatch(s, "Using old configuration tools.+")),
                            "Missing \"old configuration tools\" warning"
                        );
                    } else {
                        // Second time through, we've fixed this warning.
                        Assert.IsNull(
                            warnings.FirstOrDefault(s => Regex.IsMatch(s, "Using old configuration tools.+")),
                            "Still received \"old configuration tools\" warning"
                        );
                    }


                    switch (iteration) {
                        case 0:
                            app.AddItem(project, PythonVisualStudioApp.TemplateLanguageName, PythonVisualStudioApp.WebRoleSupportTemplate, "bin");
                            break;
                        case 1:
                            var model = app.GetService<IComponentModel>(typeof(SComponentModel));
                            var interpreterService = model.GetService<IInterpreterOptionsService>();
                            var newInterpreter = interpreterService.FindInterpreter(PythonPaths.CPythonGuid, "3.4")
                                ?? interpreterService.FindInterpreter(PythonPaths.CPythonGuid, "2.7");
                            Assert.IsNotNull(newInterpreter);
                            interpreterService.DefaultInterpreter = newInterpreter;
                            break;
                    }
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectAddSupportFiles() {
            using (var app = new VisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.EmptyWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectAddSupportFiles"
                );

                var proj = project.GetCommonProject();
                Assert.IsNotNull(proj);

                var previousItems = project.ProjectItems.Cast<ProjectItem>().Select(p => p.Name).ToSet(StringComparer.CurrentCultureIgnoreCase);

                // Add the items
                app.AddItem(project, PythonVisualStudioApp.TemplateLanguageName, PythonVisualStudioApp.WebRoleSupportTemplate, "bin");

                var newItems = project.ProjectItems.Cast<ProjectItem>().Where(p => !previousItems.Contains(p.Name)).ToList();
                AssertUtil.ContainsExactly(newItems.Select(i => i.Name), "bin");

                var children = newItems[0].ProjectItems.Cast<ProjectItem>().Select(i => i.Name).ToSet(StringComparer.CurrentCultureIgnoreCase);
                AssertUtil.ContainsExactly(children, "ConfigureCloudService.ps1", "ps.cmd", "Readme.txt");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WorkerProjectAddSupportFiles() {
            using (var app = new VisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.PythonApplicationTemplate,
                    TestData.GetTempPath(),
                    "WorkerProjectAddSupportFiles"
                );

                // Ensure the bin directory already exists
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(project.FullName), "bin"));

                var previousItems = project.ProjectItems.Cast<ProjectItem>().Select(p => p.Name).ToSet(StringComparer.CurrentCultureIgnoreCase);

                // Add the items
                app.AddItem(project, PythonVisualStudioApp.TemplateLanguageName, PythonVisualStudioApp.WorkerRoleSupportTemplate, "bin");

                var newItems = project.ProjectItems.Cast<ProjectItem>().Where(p => !previousItems.Contains(p.Name)).ToList();
                AssertUtil.ContainsExactly(newItems.Select(i => i.Name), "bin");

                var children = newItems[0].ProjectItems.Cast<ProjectItem>().Select(i => i.Name).ToSet(StringComparer.CurrentCultureIgnoreCase);
                AssertUtil.ContainsExactly(children, "ConfigureCloudService.ps1", "LaunchWorker.ps1", "ps.cmd", "Readme.txt");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectCreateVirtualEnvOnNew() {
            using (var app = new VisualStudioApp()) {
                var t = Task.Run(() => app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.FlaskWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectCreateVirtualEnvOnNew",
                    suppressUI: false
                ));

                using (var dlg = new AutomationDialog(app, AutomationElement.FromHandle(app.WaitForDialog(t)))) {
                    // Create a virtual environment
                    dlg.ClickButtonAndClose("CommandLink_1000", nameIsAutomationId: true);
                }

                using (var dlg = new AutomationDialog(app, AutomationElement.FromHandle(app.WaitForDialog(t)))) {
                    dlg.ClickButtonByAutomationId("Create");
                    dlg.ClickButtonAndClose("Close", nameIsAutomationId: true);
                }
                
                t.WaitAndUnwrapExceptions();
                var project = t.Result;

                var provider = project.Properties.Item("InterpreterFactoryProvider").Value as MSBuildProjectInterpreterFactoryProvider;
                for (int retries = 20; retries > 0; --retries) {
                    if (provider.IsProjectSpecific(provider.ActiveInterpreter)) {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                Assert.IsTrue(provider.IsProjectSpecific(provider.ActiveInterpreter), "Did not have virtualenv");
                
                for (int retries = 60; retries > 0; --retries) {
                    if (InterpreterExt.FindModules(provider.ActiveInterpreter, "flask").Any()) {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                AssertUtil.ContainsExactly(
                    InterpreterExt.FindModules(provider.ActiveInterpreter, "flask"),
                    "flask"
                );
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void WebProjectInstallOnNew() {
            using (var app = new PythonVisualStudioApp()) {
                Pip.Uninstall(app.InterpreterService.DefaultInterpreter, "bottle", false).WaitAndUnwrapExceptions();

                var t = Task.Run(() => app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.BottleWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectInstallOnNew",
                    suppressUI: false
                ));

                using (var dlg = new AutomationDialog(app, AutomationElement.FromHandle(app.WaitForDialog(t)))) {
                    // Install to active environment
                    dlg.ClickButtonAndClose("CommandLink_1001", nameIsAutomationId: true);
                }

                t.WaitAndUnwrapExceptions();
                var project = t.Result;

                var provider = project.Properties.Item("InterpreterFactoryProvider").Value as MSBuildProjectInterpreterFactoryProvider;

                Assert.AreSame(app.InterpreterService.DefaultInterpreter, provider.ActiveInterpreter);

                for (int retries = 60; retries > 0; --retries) {
                    if (InterpreterExt.FindModules(provider.ActiveInterpreter, "bottle").Any()) {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                AssertUtil.ContainsExactly(
                    InterpreterExt.FindModules(provider.ActiveInterpreter, "bottle"),
                    "bottle"
                );

                Pip.Uninstall(app.InterpreterService.DefaultInterpreter, "bottle", false).WaitAndUnwrapExceptions();
            }
        }

        private static void CloudProjectTest(string roleType, bool openServiceDefinition) {
            Assert.IsTrue(roleType == "Web" || roleType == "Worker", "Invalid roleType: " + roleType);

            var asm = Assembly.Load("Microsoft.VisualStudio.CloudService.Wizard,Version=1.0.0.0,Culture=neutral,PublicKeyToken=b03f5f7f11d50a3a");
            
            if (asm != null && asm.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)
                .OfType<AssemblyFileVersionAttribute>()
                .Any(a => {
                    Version ver;
                    return Version.TryParse(a.Version, out ver) && ver < new Version(2, 4);
                })
            ) {
                Assert.Inconclusive("Test requires Microsoft Azure Tools 2.4 or later");
            }

            using (var app = new VisualStudioApp())
            using (FileUtils.Backup(TestData.GetPath(@"TestData\CloudProject\ServiceDefinition.csdef"))) {
                app.OpenProject("TestData\\CloudProject.sln", expectedProjects: 3);

                var ccproj = app.Dte.Solution.Projects.Cast<Project>().FirstOrDefault(p => p.Name == "CloudProject");
                Assert.IsNotNull(ccproj);

                if (openServiceDefinition) {
                    var wnd = ccproj.ProjectItems.Item("ServiceDefinition.csdef").Open();
                    wnd.Activate();
                    app.OnDispose(() => wnd.Close());
                }

                IVsHierarchy hier;
                var sln = app.GetService<IVsSolution>(typeof(SVsSolution));
                ErrorHandler.ThrowOnFailure(sln.GetProjectOfUniqueName(ccproj.FullName, out hier));

                UIThread.Invoke(() =>
                    PythonProjectNode.UpdateServiceDefinition(hier, roleType, roleType + "Role1", app.ServiceProvider)
                );

                var doc = new XmlDocument();
                for (int retries = 5; retries > 0; --retries) {
                    try {
                        doc.Load(TestData.GetPath(@"TestData\CloudProject\ServiceDefinition.csdef"));
                        break;
                    } catch (IOException ex) {
                        Console.WriteLine("Exception while reading ServiceDefinition.csdef.{0}{1}", Environment.NewLine, ex);
                    } catch (XmlException) {
                        var copyTo = TestData.GetPath(@"TestData\CloudProject\" + Path.GetRandomFileName());
                        File.Copy(TestData.GetPath(@"TestData\CloudProject\ServiceDefinition.csdef"), copyTo);
                        Console.WriteLine("Copied file to " + copyTo);
                        throw;
                    }
                    Thread.Sleep(100);
                }
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");
                doc.Save(Console.Out);

                var nav = doc.CreateNavigator();
                if (roleType == "Web") {
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WebRole[@name='WebRole1']/sd:Startup/sd:Task[@commandLine='ps.cmd ConfigureCloudService.ps1']",
                        ns
                    ));
                } else if (roleType == "Worker") {
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WorkerRole[@name='WorkerRole1']/sd:Startup/sd:Task[@commandLine='bin\\ps.cmd ConfigureCloudService.ps1']",
                        ns
                    ));
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WorkerRole[@name='WorkerRole1']/sd:Runtime/sd:EntryPoint/sd:ProgramEntryPoint[@commandLine='bin\\ps.cmd LaunchWorker.ps1']",
                        ns
                    ));
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UpdateWebRoleServiceDefinitionInVS() {
            CloudProjectTest("Web", false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UpdateWorkerRoleServiceDefinitionInVS() {
            CloudProjectTest("Worker", false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UpdateWebRoleServiceDefinitionInVSDocumentOpen() {
            CloudProjectTest("Web", true);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UpdateWorkerRoleServiceDefinitionInVSDocumentOpen() {
            CloudProjectTest("Worker", true);
        }

        #region EndToEndTest

        private static void EndToEndLog(string format, params object[] args) {
            Console.Write("[{0:o}] ", DateTime.Now);
            if (args != null && args.Length > 0) {
                Console.WriteLine(format, args);
            } else {
                Console.WriteLine(format);
            }
        }

        private void EndToEndTest(
            string templateName,
            string moduleName,
            string textInResponse,
            string pythonVersion,
            string packageName = null
        ) {
            EndToEndLog("Starting {0} {1}", templateName, pythonVersion);
            using (var app = new VisualStudioApp()) {
                var pyProj = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    templateName,
                    TestData.GetTempPath(),
                    TestContext.TestName
                ).GetPythonProject();

                EndToEndLog("Created project {0}", pyProj.ProjectFile);

                Assert.IsInstanceOfType(pyProj.GetLauncher(), typeof(PythonWebLauncher));

                var factory = CreateVirtualEnvironment(pythonVersion, app, pyProj);

                EndToEndLog("Created virtual environment {0}", factory.Description);

                InstallWebFramework(moduleName, packageName ?? moduleName, factory);

                EndToEndLog("Installed framework {0}", moduleName);

                // Abort analysis so we don't have too many python.exe processes
                // floating around.
                foreach (var p in Process.GetProcessesByName("Microsoft.PythonTools.Analyzer")) {
                    p.Kill();
                }

                EndToEndLog("Aborted analysis");

                UIThread.Invoke(() => {
                    pyProj.SetProjectProperty("WebBrowserPort", "23457");
                });
                EndToEndLog("Set WebBrowserPort to 23457");
                LaunchAndVerifyNoDebug(app, 23457, textInResponse);
                EndToEndLog("Verified without debugging");

                UIThread.Invoke(() => {
                    pyProj.SetProjectProperty("WebBrowserPort", "23456");
                });
                EndToEndLog("Set WebBrowserPort to 23456");
                LaunchAndVerifyDebug(app, 23456, textInResponse);
                EndToEndLog("Verified with debugging");
            }
        }


        private static void LaunchAndVerifyDebug(VisualStudioApp app, int port, string textInResponse) {
            EndToEndLog("Building");
            app.Dte.Solution.SolutionBuild.Build(true);
            EndToEndLog("Starting debugging");
            if (!System.Threading.Tasks.Task.Run(() => app.Dte.Debugger.Go(false)).Wait(TimeSpan.FromSeconds(10))) {
                Assert.Fail("Run was interrupted by dialog");
            }
            EndToEndLog("Debugging started");

            string text = string.Empty;
            int retries;
            try {
                for (retries = 100;
                    retries > 0 &&
                        (app.Dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgRunMode ||
                        !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port));
                    --retries) {
                    Thread.Sleep(300);
                }

                if (retries > 0) {
                    EndToEndLog("Active at http://localhost:{0}/", port);
                } else {
                    EndToEndLog("Timed out waiting for http://localhost:{0}/", port);
                }
                text = WebDownloadUtility.GetString(new Uri(string.Format("http://localhost:{0}/", port)));
            } finally {
                app.Dte.Debugger.Stop();
            }

            EndToEndLog("Response from http://localhost:{0}/", port);
            EndToEndLog(text);
            Assert.IsTrue(text.Contains(textInResponse), text);

            for (retries = 10;
                retries > 0 &&
                    (app.Dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgRunMode ||
                    !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().All(p => p.Port != port));
                --retries) {
                Thread.Sleep(500);
            }
            if (retries > 0) {
                EndToEndLog("Debugging stopped");
            } else {
                EndToEndLog("Timed out waiting for debugging to stop");
            }
        }

        private static void LaunchAndVerifyNoDebug(
            VisualStudioApp app,
            int port,
            string textInResponse
        ) {
            var pythonProcessIds = new HashSet<int>(Process.GetProcessesByName("python").Select(p => p.Id));
            Process[] newProcesses;
            bool prevNormal = true, prevAbnormal = true;
            int retries;

            try {
                EndToEndLog("Transitioning to UI thread to build");
                UIThread.Invoke(() => {
                    EndToEndLog("Building");
                    app.Dte.Solution.SolutionBuild.Build(true);
                    EndToEndLog("Updating settings");
                    prevNormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit;
                    prevAbnormal = PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = false;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = false;

                    EndToEndLog("Starting running");
                    app.Dte.Solution.SolutionBuild.Run();
                    EndToEndLog("Running");
                });

                newProcesses = new Process[0];
                for (int i = 20;
                    i > 0 && !newProcesses.Any();
                    --i, newProcesses = Process.GetProcessesByName("python").Where(p => !pythonProcessIds.Contains(p.Id)).ToArray()) {
                    Thread.Sleep(500);
                }
                Assert.IsTrue(newProcesses.Any(), "Did not find new Python process");
                EndToEndLog("Found new processes with IDs {0}", string.Join(", ", newProcesses.Select(p => p.Id.ToString())));

                for (retries = 100;
                    retries > 0 &&
                        !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port);
                    --retries) {
                    Thread.Sleep(300);
                }
                EndToEndLog("Active at http://localhost:{0}/", port);
            } finally {
                UIThread.Invoke(() => {
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = prevNormal;
                    PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = prevAbnormal;
                });
            }
            var text = WebDownloadUtility.GetString(new Uri(string.Format("http://localhost:{0}/", port)));

            for (retries = 10; retries >= 0; --retries) {
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
                        EndToEndLog("Failed to kill {0}", newProcesses[j]);
                        EndToEndLog(ex.ToString() + Environment.NewLine);
                        allKilled = false;
                    }
                }
                if (allKilled) {
                    break;
                }
                Thread.Sleep(100);
                Assert.AreNotEqual(0, retries, "Failed to kill process.");
            }

            EndToEndLog("Response from http://localhost:{0}/", port);
            EndToEndLog(text);
            Assert.IsTrue(text.Contains(textInResponse), text);

            for (retries = 10;
                retries > 0 && !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().All(p => p.Port != port);
                --retries) {
                Thread.Sleep(500);
            }
            if (retries > 0) {
                EndToEndLog("Process ended");
            } else {
                EndToEndLog("Timed out waiting for process to exit");
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
                Version.Parse(pythonVersion) >= new Version(3, 3)
            );
            task.Wait();
            var factory = task.Result;
            Assert.IsTrue(factory.Id == pyProj.GetInterpreterFactory().Id);
            return factory;
        }

        internal static void InstallWebFramework(string moduleName, string packageName, IPythonInterpreterFactory factory) {
            Pip.Install(factory, packageName, false).WaitAndUnwrapExceptions();
            Assert.AreEqual(1, InterpreterExt.FindModules(factory, moduleName).Count);
        }

        #endregion

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void FlaskEndToEndV34() {
            EndToEndTest(PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "3.4");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void FlaskEndToEndV27() {
            EndToEndTest(PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void BottleEndToEndV34() {
            EndToEndTest(PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "3.4");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void BottleEndToEndV27() {
            EndToEndTest(PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "2.7");
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void DjangoEndToEndV27() {
            EndToEndTest(
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "2.7"
            );
        }

        [TestMethod, Priority(0), TestCategory("Core"), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost")]
        public void DjangoEndToEndV34() {
            EndToEndTest(
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "3.4"
            );
        }
    }
}

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Xml;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using pythontools::Microsoft.VisualStudio.Azure;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Thread = System.Threading.Thread;

namespace PythonToolsUITests {
    //[TestClass]
    public class WebProjectTests {
        public TestContext TestContext { get; set; }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void LoadWebFlavoredProject(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\EmptyWebProject.sln");
            Assert.AreEqual("EmptyWebProject.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

            var catids = app.Dte.ObjectExtenders.GetContextualExtenderCATIDs();
            dynamic extender = project.Extender["WebApplication"];
            Assert.IsNotNull(extender, "No WebApplication extender");
            extender.StartWebServerOnDebug = true;
            extender.StartWebServerOnDebug = false;

            var proj = project.GetCommonProject();
            var ccp = proj as IPythonProject;
            Assert.IsNotNull(ccp);
            Assert.IsNotNull(ccp.FindCommand("PythonRunWebServerCommand"), "No PythonRunWebServerCommand");
            Assert.IsNotNull(ccp.FindCommand("PythonDebugWebServerCommand"), "No PythonDebugWebServerCommand");
        }

        private static void CheckCommandLineArgs(PythonVisualStudioApp app, string setValue, string expectedValue = null) {
            var project = app.OpenProject(@"TestData\CheckCommandLineArgs.sln");

            var proj = project.GetCommonProject() as IPythonProject;
            Assert.IsNotNull(proj);

            var outFile = Path.Combine(Path.GetDirectoryName(project.FullName), "output.txt");

            foreach (var cmdName in new[] { "PythonRunWebServerCommand", "PythonDebugWebServerCommand" }) {
                Console.WriteLine("Testing {0}, writing to {1}", cmdName, outFile);

                if (File.Exists(outFile)) {
                    File.Delete(outFile);
                }

                app.ServiceProvider.GetUIThread().Invoke(() => {
                    proj.SetProperty("CommandLineArguments", string.Format("\"{0}\" \"{1}\"", setValue, outFile));
                    proj.FindCommand(cmdName).Execute(proj);
                });

                for (int retries = 10; retries > 0 && !File.Exists(outFile); --retries) {
                    Thread.Sleep(100);
                }

                Assert.AreEqual(expectedValue ?? setValue, File.ReadAllText(outFile).Trim());
            }
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectCommandLineArgs(PythonVisualStudioApp app) {
            CheckCommandLineArgs(app, Guid.NewGuid().ToString("N"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectStartupModuleArgs(PythonVisualStudioApp app) {
            CheckCommandLineArgs(app, "{StartupModule}", "CheckCommandLineArgs");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectEnvironment(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\CheckEnvironment.sln");

            var proj = project.GetCommonProject() as IPythonProject;
            Assert.IsNotNull(proj);

            var outFile = Path.Combine(Path.GetDirectoryName(project.FullName), "output.txt");
            if (File.Exists(outFile)) {
                File.Delete(outFile);
            }

            app.ServiceProvider.GetUIThread().Invoke(() => {
                proj.SetProperty("CommandLineArguments", '"' + outFile + '"');
                proj.SetProperty("Environment", "FOB=123\nOAR=456\r\nBAZ=789");
            });

            app.ExecuteCommand("Debug.StartWithoutDebugging");

            for (int retries = 10; retries > 0 && !File.Exists(outFile); --retries) {
                Thread.Sleep(300);
            }

            Assert.AreEqual("FOB=123\r\nOAR=456\r\nBAZ=789", File.ReadAllText(outFile).Trim());
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectStaticUri(PythonVisualStudioApp app) {
            var project = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                PythonVisualStudioApp.EmptyWebProjectTemplate,
                TestData.GetTempPath(),
                "WebProjectStaticUri"
            );

            var proj = project.GetCommonProject();
            Assert.IsNotNull(proj);

            app.ServiceProvider.GetUIThread().Invoke(() => {
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

            app.ServiceProvider.GetUIThread().Invoke(() => {
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

            app.ServiceProvider.GetUIThread().Invoke(() => {
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

            app.ServiceProvider.GetUIThread().Invoke(() => {
                proj.SetProjectProperty("StaticUriPattern", "invalid[pattern");
            });
            app.ExecuteCommand("Build.RebuildSolution");
            app.WaitForOutputWindowText("Build", "1 failed");
        }

        // Update the test from version 3.3/3.4 to 3.5-3.7. 
        /*
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectBuildWarnings(PythonVisualStudioApp app) {
            using (app.SelectDefaultInterpreter(PythonPaths.Python33 ?? PythonPaths.Python33_x64)) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.EmptyWebProjectTemplate,
                    TestData.GetTempPath(),
                    "WebProjectBuildWarnings"
                );

                var proj = project.GetPythonProject();
                Assert.IsNotNull(proj);
                Assert.AreEqual(new Version(3, 3), app.ServiceProvider.GetUIThread().Invoke(() => proj.GetLaunchConfigurationOrThrow().Interpreter.Version));

                for (int iteration = 0; iteration <= 2; ++iteration) {
                    var warnings = app.ServiceProvider.GetUIThread().Invoke(() => {
                        var buildPane = app.GetOutputWindow("Build");
                        buildPane.Clear();

                        project.DTE.Solution.SolutionBuild.Clean(true);
                        project.DTE.Solution.SolutionBuild.Build(true);

                        var text = app.GetOutputWindowText("Build");
                        Console.WriteLine(text);
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
                            var interpreterService = model.GetService<IInterpreterRegistryService>();
                            var optionsService = model.GetService<IInterpreterOptionsService>();
                            var newInterpreter = interpreterService.FindInterpreter("Global|PythonCore|3.4|x86")
                                ?? interpreterService.FindInterpreter("Global|PythonCore|2.7|x86");
                            Assert.IsNotNull(newInterpreter);
                            optionsService.DefaultInterpreter = newInterpreter;
                            break;
                    }
                }
            }
        }*/

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectAddSupportFiles(PythonVisualStudioApp app) {
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
            AssertUtil.ContainsExactly(children, "ConfigureCloudService.ps1", "ps.cmd", "readme.html");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WorkerProjectAddSupportFiles(PythonVisualStudioApp app) {
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
            AssertUtil.ContainsExactly(children, "ConfigureCloudService.ps1", "LaunchWorker.ps1", "ps.cmd", "readme.html");
        }

        private static void InstallModule(PythonVisualStudioApp app, IPythonInterpreterFactory factory, string module) {
            var pm = app.OptionsService.GetPackageManagers(factory).First();
            pm.InstallAsync(new PackageSpec(module), new TestPackageManagerUI(), CancellationTokens.After60s).WaitAndUnwrapExceptions();
        }

        private static void UninstallModule(PythonVisualStudioApp app, IPythonInterpreterFactory factory, string module) {
            var pm = app.OptionsService.GetPackageManagers(factory).First();
            pm.UninstallAsync(new PackageSpec(module), new TestPackageManagerUI(), CancellationTokens.After60s).WaitAndUnwrapExceptions();
        }

        private static bool HasModule(PythonVisualStudioApp app, IPythonInterpreterFactory factory, string module) {
            var pm = app.OptionsService.GetPackageManagers(factory).First();
            return pm.GetInstalledPackageAsync(new PackageSpec(module), CancellationTokens.After60s).WaitAndUnwrapExceptions().IsValid;
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectCreateVirtualEnvOnNew(PythonVisualStudioApp app) {
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

            var project = t.WaitAndUnwrapExceptions();

            var contextProvider = app.ComponentModel.GetService<VsProjectContextProvider>();
            for (int retries = 20; retries > 0; --retries) {
                if (contextProvider.IsProjectSpecific(project.GetPythonProject().ActiveInterpreter.Configuration)) {
                    break;
                }
                Thread.Sleep(1000);
            }
            Assert.IsTrue(contextProvider.IsProjectSpecific(project.GetPythonProject().ActiveInterpreter.Configuration), "Did not have virtualenv");

            for (int retries = 60; retries > 0; --retries) {
                if (HasModule(app, project.GetPythonProject().ActiveInterpreter, "flask")) {
                    break;
                }
                Thread.Sleep(1000);
            }
            Assert.IsTrue(HasModule(app, project.GetPythonProject().ActiveInterpreter, "flask"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void WebProjectInstallOnNew(PythonVisualStudioApp app) {
            UninstallModule(app, app.OptionsService.DefaultInterpreter, "bottle");

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

            var project = t.WaitAndUnwrapExceptions();

            Assert.AreSame(app.OptionsService.DefaultInterpreter, project.GetPythonProject().ActiveInterpreter);

            for (int retries = 60; retries > 0; --retries) {
                if (HasModule(app, project.GetPythonProject().ActiveInterpreter, "bottle")) {
                    break;
                }
                Thread.Sleep(1000);
            }
            Assert.IsTrue(HasModule(app, project.GetPythonProject().ActiveInterpreter, "bottle"));

            UninstallModule(app, app.OptionsService.DefaultInterpreter, "bottle");
        }

        private static void CloudProjectTest(PythonVisualStudioApp app, string roleType, bool openServiceDefinition) {
            Assert.IsTrue(roleType == "Web" || roleType == "Worker", "Invalid roleType: " + roleType);

            Assembly asm = null;
            try {
                asm = Assembly.Load("Microsoft.VisualStudio.CloudService.Wizard,Version=1.0.0.0,Culture=neutral,PublicKeyToken=b03f5f7f11d50a3a");
            } catch {
                // Failed to load - we'll skip the test below
            }

            if (asm != null && asm.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)
                .OfType<AssemblyFileVersionAttribute>()
                .Any(a => {
                    Version ver;
                    return Version.TryParse(a.Version, out ver) && ver < new Version(2, 5);
                })
            ) {
                Assert.Inconclusive("Test requires Microsoft Azure Tools 2.5 or later");
            }

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

                var pyproj = app.Dte.Solution.Projects.Cast<Project>().FirstOrDefault(p => p.Name == roleType + "Role1");
                Assert.IsNotNull(pyproj);
                app.ServiceProvider.GetUIThread().InvokeAsync(() => {
                    Assert.IsNotNull(pyproj.GetPythonProject());
                    ((IAzureRoleProject)pyproj.GetPythonProject()).AddedAsRole(hier, roleType);
                }).GetAwaiter().GetResult();

                // AddedAsRole runs in the background, so wait a second for it to
                // do its thing.
                Thread.Sleep(1000);

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
                        "/sd:ServiceDefinition/sd:WorkerRole[@name='WorkerRole1']/sd:Runtime/sd:EntryPoint/sd:ProgramEntryPoint[@commandLine='bin\\ps.cmd LaunchWorker.ps1 worker.py']",
                        ns
                    ));
                }
            }
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void UpdateWebRoleServiceDefinitionInVS(PythonVisualStudioApp app) {
            CloudProjectTest(app, "Web", false);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void UpdateWorkerRoleServiceDefinitionInVS(PythonVisualStudioApp app) {
            CloudProjectTest(app, "Worker", false);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void UpdateWebRoleServiceDefinitionInVSDocumentOpen(PythonVisualStudioApp app) {
            CloudProjectTest(app, "Web", true);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void UpdateWorkerRoleServiceDefinitionInVSDocumentOpen(PythonVisualStudioApp app) {
            CloudProjectTest(app, "Worker", true);
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
            PythonVisualStudioApp app,
            string templateName,
            string moduleName,
            string textInResponse,
            string pythonVersion,
            string packageName = null
        ) {
            EndToEndLog("Starting {0} {1}", templateName, pythonVersion);
            var pyProj = app.CreateProject(
                PythonVisualStudioApp.TemplateLanguageName,
                templateName,
                TestData.GetTempPath(),
                TestContext.TestName
            ).GetPythonProject();

            EndToEndLog("Created project {0}", pyProj.ProjectFile);

            Assert.IsInstanceOfType(pyProj.GetLauncher(), typeof(PythonWebLauncher));

            IPythonInterpreterFactory factory;

            // Abort analysis so we don't have too many python.exe processes
            // floating around.
            using (new ProcessScope("Microsoft.PythonTools.Analyzer")) {
                factory = CreateVirtualEnvironment(pythonVersion, app, pyProj);

                EndToEndLog("Created virtual environment {0}", factory.Configuration.Description);

                InstallWebFramework(app, moduleName, packageName ?? moduleName, factory);

                EndToEndLog("Installed framework {0}", moduleName);
            }

            EndToEndLog("Aborted analysis");

            app.ServiceProvider.GetUIThread().Invoke(() => {
                pyProj.SetProjectProperty("WebBrowserUrl", "");
                pyProj.SetProjectProperty("WebBrowserPort", "23457");
            });
            EndToEndLog("Set WebBrowserPort to 23457");
            LaunchAndVerifyNoDebug(app, 23457, textInResponse);
            EndToEndLog("Verified without debugging");

            app.ServiceProvider.GetUIThread().Invoke(() => {
                pyProj.SetProjectProperty("WebBrowserUrl", "");
                pyProj.SetProjectProperty("WebBrowserPort", "23456");
            });
            EndToEndLog("Set WebBrowserPort to 23456");
            LaunchAndVerifyDebug(app, 23456, textInResponse);
            EndToEndLog("Verified with debugging");
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

            for (retries = 20;
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
            bool prevNormal = true, prevAbnormal = true;
            string text;
            int retries;

            try {
                using (var processes = new ProcessScope("python")) {
                    EndToEndLog("Transitioning to UI thread to build");
                    app.ServiceProvider.GetUIThread().Invoke(() => {
                        EndToEndLog("Building");
                        app.Dte.Solution.SolutionBuild.Build(true);
                        EndToEndLog("Build output: {0}", app.GetOutputWindowText("Build"));
                        EndToEndLog("Updating settings");
                        prevNormal = app.GetService<PythonToolsService>().DebuggerOptions.WaitOnNormalExit;
                        prevAbnormal = app.GetService<PythonToolsService>().DebuggerOptions.WaitOnAbnormalExit;
                        app.GetService<PythonToolsService>().DebuggerOptions.WaitOnNormalExit = false;
                        app.GetService<PythonToolsService>().DebuggerOptions.WaitOnAbnormalExit = false;

                        EndToEndLog("Starting running");
                        app.Dte.Solution.SolutionBuild.Run();
                        EndToEndLog("Running");
                    });

                    var newProcesses = processes.WaitForNewProcess(TimeSpan.FromSeconds(30)).ToList();
                    Assert.IsTrue(newProcesses.Any(), "Did not find new Python process");
                    EndToEndLog("Found new processes with IDs {0}", string.Join(", ", newProcesses.Select(p => p.Id.ToString())));

                    for (retries = 100;
                        retries > 0 &&
                            !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port);
                        --retries) {
                        Thread.Sleep(300);
                    }
                    EndToEndLog("Active at http://localhost:{0}/", port);

                    text = WebDownloadUtility.GetString(new Uri(string.Format("http://localhost:{0}/", port)));
                }
            } finally {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.GetService<PythonToolsService>().DebuggerOptions.WaitOnNormalExit = prevNormal;
                    app.GetService<PythonToolsService>().DebuggerOptions.WaitOnAbnormalExit = prevAbnormal;
                });
            }

            EndToEndLog("Response from http://localhost:{0}/", port);
            EndToEndLog(text);
            Assert.IsTrue(text.Contains(textInResponse), text);

            for (retries = 20;
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
            var uiThread = app.ServiceProvider.GetUIThread();
            var task = uiThread.InvokeTask(() => {
                var model = app.GetService<IComponentModel>(typeof(SComponentModel));
                var registryService = model.GetService<IInterpreterRegistryService>();
                var optionsService = model.GetService<IInterpreterOptionsService>();

                return VirtualEnv.CreateAndAddFactory(
                    app.ServiceProvider,
                    registryService,
                    optionsService,
                    pyProj,
                    null,
                    Path.Combine(pyProj.ProjectHome, "env"),
                    registryService.FindInterpreter("Global|PythonCore|" + pythonVersion + "-32"),
                    false,
                    null,
                    Version.Parse(pythonVersion) >= new Version(3, 3)
                );
            });
            try {
                Assert.IsTrue(task.Wait(TimeSpan.FromMinutes(2.0)), "Timed out waiting for venv");
            } catch (AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            var factory = task.Result;
            Assert.IsTrue(uiThread.Invoke(() => factory.Configuration.Id == pyProj.GetInterpreterFactory().Configuration.Id));
            return factory;
        }

        class TraceRedirector : Redirector {
            private readonly string _prefix;

            public TraceRedirector(string prefix = "") {
                if (string.IsNullOrEmpty(prefix)) {
                    _prefix = "";
                } else {
                    _prefix = prefix + ": ";
                }
            }

            public override void WriteLine(string line) {
                Trace.WriteLine(_prefix + line);
            }

            public override void WriteErrorLine(string line) {
                Trace.WriteLine(_prefix + "[ERROR] " + line);
            }
        }

        internal static void InstallWebFramework(PythonVisualStudioApp app, string moduleName, string packageName, IPythonInterpreterFactory factory) {
            InstallModule(app, factory, packageName);
            Assert.IsTrue(HasModule(app, factory, moduleName));
        }

        #endregion

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FlaskEndToEndV34(PythonVisualStudioApp app) {
            EndToEndTest(app, PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "3.4");
        }

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FlaskEndToEndV27(PythonVisualStudioApp app) {
            EndToEndTest(app, PythonVisualStudioApp.FlaskWebProjectTemplate, "flask", "Hello World!", "2.7");
        }

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void BottleEndToEndV34(PythonVisualStudioApp app) {
            EndToEndTest(app, PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "3.4");
        }

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void BottleEndToEndV27(PythonVisualStudioApp app) {
            EndToEndTest(app, PythonVisualStudioApp.BottleWebProjectTemplate, "bottle", "<b>Hello world</b>!", "2.7");
        }

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoEndToEndV27(PythonVisualStudioApp app) {
            EndToEndTest(
                app,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "2.7"
            );
        }

        //[TestMethod, Priority(0), Timeout(10 * 60 * 1000)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoEndToEndV34(PythonVisualStudioApp app) {
            EndToEndTest(
                app,
                PythonVisualStudioApp.DjangoWebProjectTemplate,
                "django",
                "Congratulations on your first Django-powered page.",
                "3.4"
            );
        }
    }
}

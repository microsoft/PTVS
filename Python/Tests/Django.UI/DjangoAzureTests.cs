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
using System.Threading;         // Ambiguous with EnvDTE.Thread.
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace DjangoUITests {
    [TestClass]
    public class DjangoAzureProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddCloudProject() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var newProjDialog = app.FileNewProject();

                newProjDialog.FocusLanguageNode();

                var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Web Project");
                djangoApp.Select();

                newProjDialog.Location = TestData.GetTempPath();
                newProjDialog.ClickOK();

                // wait for new solution to load...
                var projItem = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name
                );
                AutomationWrapper.Select(projItem);
                System.Threading.Thread.Sleep(1000);

                Exception exception = null;
                ThreadPool.QueueUserWorkItem(x => {
                    try {
                        app.Dte.ExecuteCommand("Project.ConverttoWindowsAzureCloudServiceProject");
                    } catch (Exception ex1) {
                        try {
                            app.Dte.ExecuteCommand("Project.AddWindowsAzureCloudServiceProject");
                        } catch (Exception ex2) {
                            Console.WriteLine("Unable to execute Project.AddWindowsAzureCloudServiceProject.\r\n{1}", ex2);
                            exception = ex1;
                        }
                    }
                });

                var res = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (2 projects)",
                    app.Dte.Solution.Projects.Item(1).Name + ".Azure"
                );

                if (exception != null) {
                    Assert.Fail("Unable to execute Project.AddWindowsAzureCloudServiceProject:\r\n{0}", exception);
                }
                Assert.IsNotNull(res);
            }
        }
    }
}

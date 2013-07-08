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
using TestUtilities.UI;

namespace DjangoUITests {
    [TestClass]
    public class DjangoAzureProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddCloudProject() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var djangoApp = newProjDialog.ProjectTypes.FindItem("Django Project");
            djangoApp.Select();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            var projItem = app.SolutionExplorerTreeView.FindItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                app.Dte.Solution.Projects.Item(1).Name
            );
            AutomationWrapper.Select(projItem);
            System.Threading.Thread.Sleep(1000);

            ThreadPool.QueueUserWorkItem(x => {
                try {
                    app.Dte.ExecuteCommand("Project.AddWindowsAzureCloudServiceProject");
                } catch(Exception e) {
                    Debug.WriteLine(e.ToString());
                }
            });

            var res = app.SolutionExplorerTreeView.WaitForItem(
                "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (2 projects)",
                app.Dte.Solution.Projects.Item(1).Name + ".Azure"
            );

            Assert.IsNotNull(res);
        }
    }
}

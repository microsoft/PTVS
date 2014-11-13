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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace DjangoUITests {
    [TestClass]
    public class DjangoAzureProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddCloudProject() {
            using (var app = new VisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.DjangoWebProjectTemplate,
                    TestData.GetTempPath(),
                    "AddCloudProject"
                );
                app.SolutionExplorerTreeView.SelectProject(project);

                try {
                    app.ExecuteCommand("Project.ConverttoMicrosoftAzureCloudServiceProject");
                } catch (Exception ex1) {
                    try {
                        app.ExecuteCommand("Project.ConverttoWindowsAzureCloudServiceProject");
                    } catch (Exception ex2) {
                        Console.WriteLine("Unable to execute Project.ConverttoWindowsAzureCloudServiceProject.\r\n{0}", ex2);
                        try {
                            app.ExecuteCommand("Project.AddWindowsAzureCloudServiceProject");
                        } catch (Exception ex3) {
                            Console.WriteLine("Unable to execute Project.AddWindowsAzureCloudServiceProject.\r\n{0}", ex3);
                            throw ex1;
                        }
                    }
                }

                var res = app.SolutionExplorerTreeView.WaitForItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (2 projects)",
                    app.Dte.Solution.Projects.Item(1).Name + ".Azure"
                );
                Assert.IsNotNull(res);
            }
        }
    }
}

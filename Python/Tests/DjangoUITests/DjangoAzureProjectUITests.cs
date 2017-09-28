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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace DjangoUITests {
    public class DjangoAzureProjectUITests {
        public void AddCloudProject(VisualStudioApp app) {
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

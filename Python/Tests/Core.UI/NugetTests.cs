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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;

namespace PythonToolsUITests {
    public class NugetTests {
        public void AddDifferentFileType(VisualStudioApp app) {
            var fullPath = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var project = app.OpenProject(fullPath);

            // "Python Environments", "References", "Search Paths", "Program.py"
            Assert.AreEqual(4, project.ProjectItems.Count);

            var item = project.ProjectItems.AddFromFileCopy(TestData.GetPath(@"TestData\Xaml\EmptyXName.xaml"));
            Assert.AreEqual("EmptyXName.xaml", item.Properties.Item("FileName").Value);
        }

        public void FileNamesResolve(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            var ps = System.Management.Automation.PowerShell.Create();
            ps.AddScript(@"
                param($project)
                $folderProjectItem = $project.ProjectItems.Item(""Program.py"")
                $result =  $folderProjectItem.FileNames(1)
            ");
            ps.AddParameter("project", project);
            ps.Invoke();
            var result = ps.Runspace.SessionStateProxy.GetVariable("result");

            var folder = project.ProjectItems.Item("Program.py");
            string path = folder.get_FileNames(1);

            Assert.AreEqual(path, result);
        }
    }
}

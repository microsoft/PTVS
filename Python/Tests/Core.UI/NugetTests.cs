/* ****************************************************************************
 *
 * Copyright (c) DEVSENSE. 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class NugetTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddDifferentFileType() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                string fullPath = TestData.GetPath(@"TestData\HelloWorld.sln");

                // "Python Environments", "References", "Search Paths", "Program.py"
                Assert.AreEqual(4, project.ProjectItems.Count);

                var item = project.ProjectItems.AddFromFileCopy(TestData.GetPath(@"TestData\Xaml\EmptyXName.xaml"));
                Assert.AreEqual("EmptyXName.xaml", item.Properties.Item("FileName").Value);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void FileNamesResolve() {
            using (var app = new VisualStudioApp()) {
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
}

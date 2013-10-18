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
using System.Linq;
using System.Xml.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project.ImportWizard;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ImportWizardTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void ImportWizardSimple() {
            var settings = new ImportSettings();

            settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filters = "*.py;*.pyproj";
            settings.SearchPaths = TestData.GetPath("TestData\\SearchPath1\\") + Environment.NewLine + TestData.GetPath("TestData\\SearchPath2\\");
            settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

            var path = settings.CreateRequestedProject();

            Assert.AreEqual(settings.ProjectPath, path);
            var proj = XDocument.Load(path);
            
            Assert.AreEqual("..\\..\\HelloWorld\\", proj.Descendant("ProjectHome").Value);
            Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", proj.Descendant("SearchPath").Value);
            AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                "Program.py");
            AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Content")).Select(x => x.Attribute("Include").Value),
                "HelloWorld.pyproj");
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFiltered() {
            var settings = new ImportSettings();

            settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filters = "*.py";
            settings.SearchPaths = TestData.GetPath("TestData\\SearchPath1\\") + Environment.NewLine + TestData.GetPath("TestData\\SearchPath2\\");
            settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

            var path = settings.CreateRequestedProject();

            Assert.AreEqual(settings.ProjectPath, path);
            var proj = XDocument.Load(path);

            Assert.AreEqual("..\\..\\HelloWorld\\", proj.Descendant("ProjectHome").Value);
            Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", proj.Descendant("SearchPath").Value);
            AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                "Program.py");
            Assert.AreEqual(0, proj.Descendants(proj.GetName("Content")).Count());
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFolders() {
            var settings = new ImportSettings();

            settings.SourcePath = TestData.GetPath("TestData\\HelloWorld2\\");
            settings.Filters = "*";
            settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

            var path = settings.CreateRequestedProject();

            Assert.AreEqual(settings.ProjectPath, path);
            var proj = XDocument.Load(path);

            Assert.AreEqual("..\\..\\HelloWorld2\\", proj.Descendant("ProjectHome").Value);
            Assert.AreEqual("", proj.Descendant("SearchPath").Value);
            AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                "Program.py",
                "TestFolder\\SubItem.py",
                "TestFolder2\\SubItem.py",
                "TestFolder3\\SubItem.py");

            AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Folder")).Select(x => x.Attribute("Include").Value),
                "TestFolder",
                "TestFolder2",
                "TestFolder3");
        }

        [TestMethod, Priority(0)]
        public void ImportWizardInterpreter() {
            var settings = new ImportSettings();

            settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filters = "*.py;*.pyproj";

            var interpreter = new PythonInterpreterView("Test", Guid.NewGuid(), new Version(2, 7), null);
            settings.AvailableInterpreters.Add(interpreter);
            settings.SelectedInterpreter = interpreter;
            settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

            var path = settings.CreateRequestedProject();

            Assert.AreEqual(settings.ProjectPath, path);
            var proj = XDocument.Load(path);

            Assert.AreEqual(interpreter.Id, Guid.Parse(proj.Descendant("InterpreterId").Value));
            Assert.AreEqual(interpreter.Version, Version.Parse(proj.Descendant("InterpreterVersion").Value));

            var interp = proj.Descendant("InterpreterReference");
            Assert.AreEqual(string.Format("{0:B}\\{1}", interpreter.Id, interpreter.Version),
                interp.Attribute("Include").Value);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardStartupFile() {
            var settings = new ImportSettings();

            settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filters = "*.py;*.pyproj";
            settings.StartupFile = "Program.py";
            settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

            var path = settings.CreateRequestedProject();

            Assert.AreEqual(settings.ProjectPath, path);
            var proj = XDocument.Load(path);

            Assert.AreEqual("Program.py", proj.Descendant("StartupFile").Value);
        }
    }
}

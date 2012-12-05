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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.ImportWizard;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class ImportWizardTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void ImportWizardSimple() {
            var wizard = new Wizard();

            var settings = new ImportSettings();
            var dict = new Dictionary<string, string>();

            settings.SourceFilesPath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filter = "*.py;*.pyproj";
            settings.SearchPaths = new[] { TestData.GetPath("TestData\\SearchPath1\\"), TestData.GetPath("TestData\\SearchPath2\\") };

            dict["$destinationdirectory$"] = TestData.GetPath("TestData\\TestDestination\\Subdirectory");

            wizard.SetReplacements(settings, dict);

            Assert.AreEqual("..\\..\\HelloWorld\\", dict["$projecthome$"]);
            Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", dict["$searchpaths$"]);
            Assert.AreEqual(@"  <ItemGroup>
    <Compile Include=""Program.py"" />
    <Content Include=""HelloWorld.pyproj"" />
  </ItemGroup>
", dict["$content$"]);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFiltered() {
            var wizard = new Wizard();

            var settings = new ImportSettings();
            var dict = new Dictionary<string, string>();

            settings.SourceFilesPath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filter = "*.py";
            settings.SearchPaths = new[] { TestData.GetPath("TestData\\SearchPath1\\"), TestData.GetPath("TestData\\SearchPath2\\") };

            dict["$destinationdirectory$"] = TestData.GetPath("TestData\\TestDestination\\Subdirectory");

            wizard.SetReplacements(settings, dict);

            Assert.AreEqual("..\\..\\HelloWorld\\", dict["$projecthome$"]);
            Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", dict["$searchpaths$"]);
            Assert.AreEqual(@"  <ItemGroup>
    <Compile Include=""Program.py"" />
  </ItemGroup>
", dict["$content$"]);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFolders() {
            var wizard = new Wizard();

            var settings = new ImportSettings();
            var dict = new Dictionary<string, string>();

            settings.SourceFilesPath = TestData.GetPath("TestData\\HelloWorld2\\");
            settings.Filter = "*";
            settings.SearchPaths = new string[0];

            dict["$destinationdirectory$"] = TestData.GetPath("TestData\\TestDestination\\Subdirectory");

            wizard.SetReplacements(settings, dict);

            Assert.AreEqual("..\\..\\HelloWorld2\\", dict["$projecthome$"]);
            Assert.AreEqual("", dict["$searchpaths$"]);
            Assert.AreEqual(@"  <ItemGroup>
    <Compile Include=""Program.py"" />
    <Compile Include=""TestFolder\SubItem.py"" />
    <Compile Include=""TestFolder2\SubItem.py"" />
    <Compile Include=""TestFolder3\SubItem.py"" />
    <Content Include=""HelloWorld2.pyproj"" />
  </ItemGroup>
  <ItemGroup>
    <Folder Include=""TestFolder"" />
    <Folder Include=""TestFolder2"" />
    <Folder Include=""TestFolder3"" />
  </ItemGroup>
", dict["$content$"]);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardInterpreter() {
            var wizard = new Wizard();

            var settings = new ImportSettings();
            var dict = new Dictionary<string, string>();

            settings.SourceFilesPath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filter = "*.py;*.pyproj";
            settings.SearchPaths = new[] { TestData.GetPath("TestData\\SearchPath1\\"), TestData.GetPath("TestData\\SearchPath2\\") };
            settings.InterpreterId = Guid.Empty.ToString();
            settings.InterpreterVersion = "2.7";

            dict["$destinationdirectory$"] = TestData.GetPath("TestData\\TestDestination\\Subdirectory");

            wizard.SetReplacements(settings, dict);

            Assert.AreEqual(@"    <InterpreterId>00000000-0000-0000-0000-000000000000</InterpreterId>
    <InterpreterVersion>2.7</InterpreterVersion>
", dict["$interpreter$"]);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardStartupFile() {
            var wizard = new Wizard();

            var settings = new ImportSettings();
            var dict = new Dictionary<string, string>();

            settings.SourceFilesPath = TestData.GetPath("TestData\\HelloWorld\\");
            settings.Filter = "*.py;*.pyproj";
            settings.SearchPaths = new[] { TestData.GetPath("TestData\\SearchPath1\\"), TestData.GetPath("TestData\\SearchPath2\\") };
            settings.StartupFile = TestData.GetPath("TestData\\HelloWorld\\Program.py");

            dict["$destinationdirectory$"] = TestData.GetPath("TestData\\TestDestination\\Subdirectory");

            wizard.SetReplacements(settings, dict);

            Assert.AreEqual(@"Program.py", dict["$startupfile$"]);
        }
    }
}

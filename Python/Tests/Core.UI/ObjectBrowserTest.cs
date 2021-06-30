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

using System;
using System.Collections.ObjectModel;
using System.Windows.Automation;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.UI;

namespace PythonToolsUITests {
    // Object browser is currently disabled
    //[TestClass]
    public class ObjectBrowserTest {

        private class NodeInfo {
            public NodeInfo(string name, string description, string[] members = null) {
                Name = name;
                Description = description;
                Members = (members != null) ? new Collection<string>(members) : new Collection<string>();
            }

            public string Name { get; private set; }

            public string Description { get; private set; }

            public Collection<string> Members { get; private set; }
        }

        private static void AssertNodes(ObjectBrowser objectBrowser, params NodeInfo[] expectedNodes) {
            AssertNodes(objectBrowser, true, expectedNodes);
        }

        private static void AssertNodes(ObjectBrowser objectBrowser, bool expand, params NodeInfo[] expectedNodes) {

            for (int i = 0; i < expectedNodes.Length; ++i) {
                // Check node name
                for (int j = 0; j < 100; j++) {
                    if (i < objectBrowser.TypeBrowserPane.Nodes.Count) {
                        break;
                    }
                    System.Threading.Thread.Sleep(250);
                }

                string str = objectBrowser.TypeBrowserPane.Nodes[i].Value.Trim();
                Console.WriteLine("Found node: {0}", str);
                Assert.AreEqual(expectedNodes[i].Name, str, "");

                objectBrowser.TypeBrowserPane.Nodes[i].Select();
                if (expand) {
                    try {
                        objectBrowser.TypeBrowserPane.Nodes[i].ExpandCollapse();
                    } catch (InvalidOperationException) {
                    }
                }

                System.Threading.Thread.Sleep(1000);

                // Check detailed node description.
                str = objectBrowser.DetailPane.Value.Trim();
                if (expectedNodes[i].Description != str) {
                    for (int j = 0; j < str.Length; j++) {
                        Console.WriteLine("{0} {1}", (int)str[j], (int)expectedNodes[i].Description[j]);
                    }
                }
                Assert.AreEqual(expectedNodes[i].Description, str, "");

                // Check dependent nodes in member pane
                int nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
                var expectedMembers = expectedNodes[i].Members;
                if (expectedMembers == null) {
                    Assert.AreEqual(0, nodeCount, "Node Count: " + nodeCount.ToString());
                } else {
                    Assert.AreEqual(expectedMembers.Count, nodeCount, "Node Count: " + nodeCount.ToString());
                    for (int j = 0; j < expectedMembers.Count; ++j) {
                        str = objectBrowser.TypeNavigatorPane.Nodes[j].Value.Trim();
                        Assert.AreEqual(expectedMembers[j], str, "");
                    }
                }
            }
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserBasicTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Outlining.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            AssertNodes(objectBrowser,
                new NodeInfo("Outlining", "Outlining"),
                new NodeInfo("BadForStatement.py", "BadForStatement.py"),
                new NodeInfo("NestedFuncDef.py", "NestedFuncDef.py", new[] { "def f()" }),
                new NodeInfo("Program.py", "Program.py", new[] { "def f()", "i" }));

            app.Dte.Solution.Close(false);

            System.Threading.Thread.Sleep(1000);
            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserSearchTextTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\ObjectBrowser.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            objectBrowser.EnsureLoaded();

            // Initially, we should have only the top-level collapsed node for the project

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            // Sanity-check the starting view with all nodes expanded.

            var expectedNodesBeforeSearch = new[] {
                    new NodeInfo("ObjectBrowser", "ObjectBrowser"),
                    new NodeInfo("Program.py", "Program.py", new[] { "def frob()" }),
                    new NodeInfo("class Fob", "class Fob"),
                    new NodeInfo("class FobOarBaz", "class FobOarBaz", new[] { "def frob(self)" }),
                    new NodeInfo("class Oar", "class Oar", new[] { "def oar(self)" }),
                };

            AssertNodes(objectBrowser, expectedNodesBeforeSearch);

            // Do the search and check results

            objectBrowser.SearchText.SetValue("oar");
            System.Threading.Thread.Sleep(1000);

            objectBrowser.SearchButton.Click();
            System.Threading.Thread.Sleep(1000);

            var expectedNodesAfterSearch = new[] {
                     new NodeInfo("oar", "def oar(self)\rdeclared in Oar"),
                     new NodeInfo("Oar", "class Oar", new[] { "def oar(self)" }),
                     new NodeInfo("FobOarBaz", "class FobOarBaz", new[] { "def frob(self)" }),
                };

            AssertNodes(objectBrowser, expectedNodesAfterSearch);

            // Clear the search and check that we get back to the starting view.

            objectBrowser.ClearSearchButton.Click();
            System.Threading.Thread.Sleep(1000);

            AssertNodes(objectBrowser, false, expectedNodesBeforeSearch);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserExpandTypeBrowserTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            objectBrowser.EnsureLoaded();

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Inheritance", str, "");
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserCommentsTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");
            objectBrowser.TypeBrowserPane.Nodes[1].Select();
            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node Count: " + nodeCount.ToString());
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.AreEqual("member", str.Trim(), "");
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("members", str.Trim(), "");
            str = objectBrowser.TypeNavigatorPane.Nodes[2].Value;
            Assert.AreEqual("s", str.Trim(), "");
            str = objectBrowser.TypeNavigatorPane.Nodes[3].Value;
            Assert.AreEqual("t", str.Trim(), "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[2].Select();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node Count: " + nodeCount.ToString());
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.IsTrue(str.Trim().StartsWith("def __init__(self"), str);
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("def tell(self)", str.Trim(), "");

            str = objectBrowser.DetailPane.Value;
            Assert.IsTrue(str.Trim().Contains("SchoolMember"), str);
            Assert.IsTrue(str.Trim().Contains("Represents any school member."), str);

            objectBrowser.TypeNavigatorPane.Nodes[1].Select();
            System.Threading.Thread.Sleep(1000);

            str = objectBrowser.DetailPane.Value;
            Assert.IsTrue(str.Trim().Contains("def tell(self)"), str);
            Assert.IsTrue(str.Trim().Contains("Tell my detail."), str);

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserInheritanceRelationshipTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].Select();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node Count: " + nodeCount.ToString());
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.IsTrue(str.Trim().StartsWith("__init__ (alias of def "), str);
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("def tell(self)", str.Trim(), "");

            str = objectBrowser.DetailPane.Value;
            Assert.IsTrue(str.Trim().Contains("Student(SchoolMember)"), str);
            Assert.IsTrue(str.Trim().Contains("Represents a student."), str);

            objectBrowser.TypeNavigatorPane.Nodes[1].Select();
            System.Threading.Thread.Sleep(1000);

            str = objectBrowser.DetailPane.Value;
            Assert.IsTrue(str.Trim().Contains("def tell(self)"), str);

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserNavigationTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            objectBrowser.EnsureLoaded();

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[4].Select();
            System.Threading.Thread.Sleep(1000);
            app.ExecuteCommand("Edit.GoToDefinition");
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("Program.py", str, "");

            int lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(14, lineNo, "Line number: " + lineNo.ToString());

            app.OpenObjectBrowser();
            objectBrowser.TypeBrowserPane.Nodes[2].Select();
            System.Threading.Thread.Sleep(1000);
            app.ExecuteCommand("Edit.GoToDefinition");
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("MyModule.py", str, "");

            lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(1, lineNo, "Line number: " + lineNo.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserContextMenuBasicTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[1].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(7, itemCount, "Item count: " + itemCount.ToString());
            Assert.AreEqual("Copy", menu.Items[0].Value.Trim(), "");
            Assert.AreEqual("View Namespaces", menu.Items[1].Value.Trim(), "");
            Assert.AreEqual("View Containers", menu.Items[2].Value.Trim(), "");
            Assert.AreEqual("Sort Alphabetically", menu.Items[3].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Type", menu.Items[4].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Access", menu.Items[5].Value.Trim(), "");
            Assert.AreEqual("Group By Object Type", menu.Items[6].Value.Trim(), "");
            Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);

            objectBrowser.TypeBrowserPane.Nodes[2].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            itemCount = menu.Items.Count;
            Assert.AreEqual(13, itemCount, "Item count: " + itemCount.ToString());
            Assert.AreEqual("Go To Definition", menu.Items[0].Value.Trim(), "");
            Assert.AreEqual("Go To Declaration", menu.Items[1].Value.Trim(), "");
            Assert.AreEqual("Go To Reference", menu.Items[2].Value.Trim(), "");
            Assert.AreEqual("Browse Definition", menu.Items[3].Value.Trim(), "");
            Assert.AreEqual("Find All References", menu.Items[4].Value.Trim(), "");
            Assert.AreEqual("Filter To Type", menu.Items[5].Value.Trim(), "");
            Assert.AreEqual("Copy", menu.Items[6].Value.Trim(), "");
            Assert.AreEqual("View Namespaces", menu.Items[7].Value.Trim(), "");
            Assert.AreEqual("View Containers", menu.Items[8].Value.Trim(), "");
            Assert.AreEqual("Sort Alphabetically", menu.Items[9].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Type", menu.Items[10].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Access", menu.Items[11].Value.Trim(), "");
            Assert.AreEqual("Group By Object Type", menu.Items[12].Value.Trim(), "");
            Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserTypeBrowserViewTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(7, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[1].Check();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            itemCount = menu.Items.Count;
            Assert.AreEqual(7, itemCount, "Item count: " + itemCount.ToString());
            Assert.IsTrue(menu.Items[1].ToggleStatus);
            menu.Items[2].Check();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserTypeBrowserSortTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(7, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[6].Check();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());
            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());
            Assert.AreEqual("Namespaces", objectBrowser.TypeBrowserPane.Nodes[3].Value, "");
            Assert.AreEqual("Namespaces", objectBrowser.TypeBrowserPane.Nodes[1].Value, "");
            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            itemCount = menu.Items.Count;
            Assert.AreEqual(7, itemCount, "Item count: " + itemCount.ToString());
            Assert.IsTrue(menu.Items[6].ToggleStatus);
            menu.Items[3].Check();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("class SchoolMember\n", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[3].Value;
            Assert.AreEqual("Program.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[4].Value;
            Assert.AreEqual("class Student(MyModule.SchoolMember)\n", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[5].Value;
            Assert.AreEqual("class Teacher(MyModule.SchoolMember)\n", str, "");
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserNavigateVarContextMenuTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            objectBrowser.EnsureLoaded();

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[4].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(13, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[0].Check();
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("Program.py", str, "");

            int lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(14, lineNo, "Line number: " + lineNo.ToString());

            app.OpenObjectBrowser();
            objectBrowser.TypeBrowserPane.Nodes[5].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            menu.Items[0].Check();
            System.Threading.Thread.Sleep(1000);

            lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(3, lineNo, "Line number: " + lineNo.ToString());
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ObjectBrowserFindAllReferencesTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(6, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[4].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(13, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[4].Check();
            System.Threading.Thread.Sleep(1000);

            //this needs to be updated for bug #4840
            str = app.Dte.ActiveWindow.Caption;
            Assert.IsTrue(str.Contains("2 matches found"), str);

            objectBrowser.TypeBrowserPane.Nodes[2].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            menu.Items[4].Check();
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveWindow.Caption;
            Assert.IsTrue(str.Contains("2 matches found"), str);
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void NavigateTo(VisualStudioApp app) {
            app.OpenProject(@"TestData\Navigation.sln");

            using (var dialog = app.OpenNavigateTo()) {
                dialog.SearchTerm = "Class";
                Assert.AreEqual(4, dialog.WaitForNumberOfResults(4));
            }

            using (var dialog = app.OpenNavigateTo()) {
                dialog.SearchTerm = "cls";
                Assert.AreEqual(4, dialog.WaitForNumberOfResults(4));
            }

            using (var dialog = app.OpenNavigateTo()) {
                dialog.SearchTerm = "func";
                Assert.AreEqual(8, dialog.WaitForNumberOfResults(8));
            }

            using (var dialog = app.OpenNavigateTo()) {
                dialog.SearchTerm = "fn";
                Assert.AreEqual(8, dialog.WaitForNumberOfResults(8));
            }
        }

        ////[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void ResourceViewIsDisabledTest(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\Outlining.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenResourceView();
            var resourceView = app.ResourceView;
            Assert.IsNotNull(resourceView);
            System.Threading.Thread.Sleep(1000);

            Assert.IsNull(resourceView.TypeBrowserPane);
        }
    }
}

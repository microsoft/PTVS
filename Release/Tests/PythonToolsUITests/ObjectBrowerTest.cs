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

using System.Windows.Automation;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;

namespace PythonToolsUITests
{
    [TestClass]
    public class ObjectBrowerTest
    {
        [ClassInitialize]
        private static void DoDeployment()
        {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup()
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    VsIdeTestHostContext.Dte.Solution.Close(false);
                    break;
                }
                catch
                {
                    VsIdeTestHostContext.Dte.Documents.CloseAll(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserBasicTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\Outlining.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());
            
            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");
            
            objectBrowser.TypeBrowserPane.Nodes[0].Select();            
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node Count: " + nodeCount.ToString());
                        
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.AreEqual("f()", str.Trim(), "");
                        
            str = objectBrowser.DetailPane.Value;
            Assert.AreEqual("Program.py", str.Trim(), "");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserSearchTextTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\Outlining.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node Count: " + nodeCount.ToString());

            objectBrowser.SearchText.SetValue("f");
            System.Threading.Thread.Sleep(1000);

            objectBrowser.SearchButton.Click();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.IsTrue(nodeCount >= 1, "Node count: " + nodeCount.ToString());
            if (objectBrowser.TypeBrowserPane.Nodes[0].Value.ToLower().Contains("namespace"))
            {
                objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
                System.Threading.Thread.Sleep(1000);
                Assert.AreEqual("f", objectBrowser.TypeBrowserPane.Nodes[1].Value.Trim(), "");
                objectBrowser.TypeBrowserPane.Nodes[1].Select();
                System.Threading.Thread.Sleep(1000);
            }
            else
            {
                Assert.AreEqual("f", objectBrowser.TypeBrowserPane.Nodes[0].Value.Trim(), "");
            }

            str = objectBrowser.DetailPane.Value;
            Assert.AreEqual("def f()", str.Trim(), "");

            objectBrowser.ClearSearchButton.Click();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node Count: " + nodeCount.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserExpandTypeBrowserTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserCommentsTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");
            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(3, nodeCount, "Node Count: " + nodeCount.ToString());
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.AreEqual("members", str.Trim(), "");
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("s", str.Trim(), "");
            str = objectBrowser.TypeNavigatorPane.Nodes[2].Value;
            Assert.AreEqual("t", str.Trim(), "");

            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[1].Select();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node Count: " + nodeCount.ToString());            
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.IsTrue(str.Trim().StartsWith("__init__(self"), str);
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("tell(self)", str.Trim(), "");

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

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserInheritanceRelationshipTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\Inheritance.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(1, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("Program.py", str, "");
            
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[2].Select();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeNavigatorPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node Count: " + nodeCount.ToString());
            str = objectBrowser.TypeNavigatorPane.Nodes[0].Value;
            Assert.IsTrue(str.Trim().StartsWith("__init__(self"), str);
            str = objectBrowser.TypeNavigatorPane.Nodes[1].Value;
            Assert.AreEqual("tell(self)", str.Trim(), "");

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

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserNavigationTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);
            
            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].Select();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[3].DoubleClick();
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("Program.py", str, "");

            int lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(14, lineNo, "Line number: " + lineNo.ToString());

            objectBrowser.TypeBrowserPane.Nodes[1].Select();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].DoubleClick();
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("MyModule.py", str, "");

            lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(1, lineNo, "Line number: " + lineNo.ToString());

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserContextMenuBasicTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
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

            objectBrowser.TypeBrowserPane.Nodes[1].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);           
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            itemCount = menu.Items.Count;
            Assert.AreEqual(11, itemCount, "Item count: " + itemCount.ToString());
            Assert.AreEqual("Go To Definition", menu.Items[0].Value.Trim(), "");
            Assert.AreEqual("Go To Declaration", menu.Items[1].Value.Trim(), "");
            Assert.AreEqual("Go To Reference", menu.Items[2].Value.Trim(), "");
            Assert.AreEqual("Find All References", menu.Items[3].Value.Trim(), "");
            Assert.AreEqual("Copy", menu.Items[4].Value.Trim(), "");
            Assert.AreEqual("View Namespaces", menu.Items[5].Value.Trim(), "");
            Assert.AreEqual("View Containers", menu.Items[6].Value.Trim(), "");
            Assert.AreEqual("Sort Alphabetically", menu.Items[7].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Type", menu.Items[8].Value.Trim(), "");
            Assert.AreEqual("Sort By Object Access", menu.Items[9].Value.Trim(), "");
            Assert.AreEqual("Group By Object Type", menu.Items[10].Value.Trim(), "");
            Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);
                        
            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserTypeBrowserViewTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");
                        
            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
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
            Assert.AreEqual(8, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);            
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);
            itemCount = menu.Items.Count;
            Assert.AreEqual(11, itemCount, "Item count: " + itemCount.ToString());
            Assert.IsTrue(menu.Items[5].ToggleStatus);
            menu.Items[6].Check();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserTypeBrowserSortTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[0].ShowContextMenu();
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
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(4, nodeCount, "Node count: " + nodeCount.ToString());
            Assert.AreEqual("Namespaces", objectBrowser.TypeBrowserPane.Nodes[3].Value, "");
            Assert.AreEqual("Namespaces", objectBrowser.TypeBrowserPane.Nodes[1].Value, "");
            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(7, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
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
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());
            
            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("SchoolMember", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[2].Value;
            Assert.AreEqual("Program.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[3].Value;
            Assert.AreEqual("Student", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[4].Value;
            Assert.AreEqual("Teacher", str, "");

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserNavigateVarContextMenuTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(11, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[0].Check();
            System.Threading.Thread.Sleep(1000);

            str = app.Dte.ActiveDocument.Name;
            Assert.AreEqual("Program.py", str, "");

            int lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(14, lineNo, "Line number: " + lineNo.ToString());

            objectBrowser.TypeBrowserPane.Nodes[4].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);            
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);            
            menu.Items[0].Check();
            System.Threading.Thread.Sleep(1000);

            lineNo = ((TextSelection)app.Dte.ActiveDocument.Selection).ActivePoint.Line;
            Assert.AreEqual(3, lineNo, "Line number: " + lineNo.ToString());

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ObjectBrowserFindAllReferencesTest()
        {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\MultiModule.sln");
            System.Threading.Thread.Sleep(1000);

            app.OpenObjectBrowser();
            var objectBrowser = app.ObjectBrowser;
            System.Threading.Thread.Sleep(1000);

            int nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(2, nodeCount, "Node count: " + nodeCount.ToString());

            string str = objectBrowser.TypeBrowserPane.Nodes[0].Value;
            Assert.AreEqual("MyModule.py", str, "");
            str = objectBrowser.TypeBrowserPane.Nodes[1].Value;
            Assert.AreEqual("Program.py", str, "");

            objectBrowser.TypeBrowserPane.Nodes[1].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);

            nodeCount = objectBrowser.TypeBrowserPane.Nodes.Count;
            Assert.AreEqual(5, nodeCount, "Node count: " + nodeCount.ToString());

            objectBrowser.TypeBrowserPane.Nodes[3].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);
            Condition con = new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ContextMenu"
                                );
            AutomationElement el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            Menu menu = new Menu(el);
            int itemCount = menu.Items.Count;
            Assert.AreEqual(11, itemCount, "Item count: " + itemCount.ToString());
            menu.Items[3].Check();
            System.Threading.Thread.Sleep(1000);
            
            //this needs to be updated for bug #4840
            str = app.Dte.ActiveWindow.Caption;
            Assert.IsTrue(str.Contains("2 matches found"), str);

            objectBrowser.TypeBrowserPane.Nodes[1].ShowContextMenu();
            System.Threading.Thread.Sleep(1000);            
            el = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, con);
            Assert.IsNotNull(el);
            menu = new Menu(el);            
            menu.Items[3].Check();
            System.Threading.Thread.Sleep(1000);
                        
            str = app.Dte.ActiveWindow.Caption;
            Assert.IsTrue(str.Contains("2 matches found"), str);

            objectBrowser.TypeBrowserPane.Nodes[2].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
            objectBrowser.TypeBrowserPane.Nodes[0].ExpandCollapse();
            System.Threading.Thread.Sleep(1000);
        }
    }
}

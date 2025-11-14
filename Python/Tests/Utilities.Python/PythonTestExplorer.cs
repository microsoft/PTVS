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
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;

namespace TestUtilities.UI {
    public class PythonTestExplorer : AutomationWrapper {
        private readonly VisualStudioApp _app;
        private readonly AutomationWrapper _searchBar;
        private PythonTestExplorerGridView _tests;

        private static class TestCommands {
            public const string GroupBy = "TestExplorer.GroupBy";
            public const string RunAllTests = "TestExplorer.RunAllTests";
            public const string CopyDetails = "TestExplorer.CopyDetails";
            public const string DebugAllTests = "TestExplorer.DebugAllTests";
        }

        public PythonTestExplorer(VisualStudioApp app, AutomationElement element, AutomationWrapper searchBarTextBox)
            : base(element) {
            _app = app;
            _searchBar = searchBarTextBox ?? throw new ArgumentNullException(nameof(searchBarTextBox));
        }

        public PythonTestExplorerGridView Tests {
            get {
                if (_tests == null) {
                    var el = this.Element.FindFirst(
                        TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "ListView"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Tracking List View"
                            )
                        )
                    );
                    if (el != null) {
                        _tests = new PythonTestExplorerGridView(el);
                    }
                }
                return _tests;
            }
        }

        /// <summary>
        /// Copies the selected test details to the clipboard.
        /// </summary>
        private void CopyDetails() {
            _app.WaitForCommandAvailable(TestCommands.CopyDetails, TimeSpan.FromSeconds(3));
            _app.ExecuteCommand(TestCommands.CopyDetails);
        }
        public string GetTestDetailSummary() {
            // Root is the Test Explorer tool window element you already have.
            var summaryControl = Element.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "SummaryControl")
            );
            if (summaryControl == null) {
                return string.Empty;
            }

            // Find the WpfTextView inside the host.
            var textView = summaryControl.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "WpfTextView")
            );
            if (textView == null) {
                return string.Empty;
            }

            // Try TextPattern.
            object p;
            if (textView.TryGetCurrentPattern(TextPattern.Pattern, out p)) {
                return ((TextPattern)p).DocumentRange.GetText(int.MaxValue);
            }
            // Fallback ValuePattern.
            if (textView.TryGetCurrentPattern(ValuePattern.Pattern, out p)) {
                return ((ValuePattern)p).Current.Value;
            }

            // Last resort: Name.
            return textView.Current.Name ?? string.Empty;
        }

        public string GetDetailsWithRetry() {
            string details = string.Empty;
            for (int i = 0; i < 5; i++) {
                var detailsTextBox = this.FindByName("Test Detail Summary");
                AutomationWrapper.CheckNullElement(detailsTextBox, "Missing: Test Detail Summary");

                // Copy to clipboard
                details = GetTestDetailSummary();

                if (details.Contains("Source:")) {
                    return details;
                }

                Thread.Sleep(500);
            }

            return details;
        }

        /// <summary>
        /// Set the grouping to namespace.
        /// </summary>
        public void GroupByProjectNamespaceClass() {
            // TODO: figure out how to programmatically change this
            // it's now a popup window that appears on TestExplorer.GroupBy command
            // well, at least when you click on it with the mouse
            // It's not coming up when invoking the command programmatically

            //var groupCommand = _app.Dte.Commands.Item(TestCommands.GroupBy);
            //Assert.IsNotNull(groupCommand, "GroupBy command not found");


            //if (!groupCommand.IsAvailable) {
            //    // Group command is not available when show hierarchy is on
            //    //_app.ExecuteCommand(TestCommands.ToggleShowTestHierarchy);
            //    _app.WaitForCommandAvailable(groupCommand, TimeSpan.FromSeconds(5));
            //}

            //_app.ExecuteCommand(TestCommands.GroupBy); // by class

            //Thread.Sleep(100);

            //var element = _app.Element.FindFirst(TreeScope.Descendants, new AndCondition(
            //    new PropertyCondition(
            //        AutomationElement.NameProperty,
            //        "Project, Namespace, Class"
            //    ),
            //    new PropertyCondition(
            //        AutomationElement.ClassNameProperty,
            //        "TextBlock"
            //    )
            //));
            //Assert.IsNotNull(element);

            //var menuItem = element.CachedParent;
            //Assert.IsNotNull(menuItem);

            //menuItem.GetInvokePattern().Invoke();

            WaitForTestsGrid();
        }

        private void WaitForTestsGrid() {
            // Wait for the test list to be created
            int retry = 10;
            while (Tests == null) {
                Thread.Sleep(250);
                retry--;
                if (retry == 0) {
                    break;
                }
            }
            Assert.IsNotNull(Tests, "Tests list is null");
        }

        public void ClearSearchBar() {
            _searchBar.SetValue("");
            Thread.Sleep(1000);
        }

        public AutomationElement WaitForItem(params string[] path) {
            // WaitForItem doesn't work well with offscreen items
            // so we use the search bar to filter by function name to 
            // limit the items on screen and then expand all tree items. 
            // Currently child items dont always load on expand, so we need to call
            // it multiple times with delay as a work around.
            _searchBar.SetValue(path[path.Length - 1]);

            for (int i = 0; i < path.Length + 2; i++) {
                Tests.ExpandAll();
            }

            return Tests.WaitForItem(path);
        }

        /// <summary>
        /// Run all tests and wait for the command to be available again.
        /// </summary>
        public void RunAll(TimeSpan timeout) {
            ClearSearchBar();
            _app.Dte.ExecuteCommand(TestCommands.RunAllTests);
            Thread.Sleep(100);
            _app.WaitForCommandAvailable(TestCommands.RunAllTests, timeout);
        }

        /// <summary>
        /// Debug all tests and wait for the command to be available again.
        /// </summary>
        public void DebugAll() {
            _app.Dte.ExecuteCommand(TestCommands.DebugAllTests);
            Thread.Sleep(100);
        }
    }
}

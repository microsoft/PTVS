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
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    public class PythonTestExplorer : AutomationWrapper {
        private readonly VisualStudioApp _app;
        private TreeView _tests;
        private SummaryPane _summary;

        private static class TestCommands {
            public const string ToggleShowTestHierarchy = "TestExplorer.ToggleShowTestHierarchy";
            public const string GroupBy = "TestExplorer.GroupBy";
            public const string NextGroupBy = "TestExplorer.NextGroupBy";
            public const string RunAllTests = "TestExplorer.RunAllTests";
        }

        public static class TestState {
            public const string NotRun = "NotRun";
            public const string Passed = "Passed";
            public const string Failed = "Failed";
        }

        public PythonTestExplorer(VisualStudioApp app, AutomationElement element)
            : base(element) {
            _app = app;
        }

        public TreeView Tests {
            get {
                if (_tests == null) {
                    var el = this.Element.FindFirst(
                        TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "TreeView"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Tests View"
                            )
                        )
                    );
                    if (el != null)
                        _tests = new TypeNavigatorPane(el);
                }
                return _tests;
            }
        }

        public SummaryPane Summary {
            get {
                if (_summary == null) {
                    var el = this.Element.FindFirst(
                        TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "ScrollViewer"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Selection Control Panel"
                            )
                        )
                    );
                    if (el != null)
                        _summary = new SummaryPane(el);
                }
                return _summary;
            }
        }

        /// <summary>
        /// Set the grouping to namespace.
        /// </summary>
        public void GroupByNamespace() {
            var groupCommand = _app.Dte.Commands.Item(TestCommands.GroupBy);
            Assert.IsNotNull(groupCommand, "GroupBy command not found");

            if (!groupCommand.IsAvailable) {
                // Group command is not available when show hierarchy is on
                _app.ExecuteCommand(TestCommands.ToggleShowTestHierarchy);
                _app.WaitForCommandAvailable(groupCommand, TimeSpan.FromSeconds(5));
            }

            _app.ExecuteCommand(TestCommands.GroupBy); // by class
            _app.ExecuteCommand(TestCommands.NextGroupBy); // by duration
            _app.ExecuteCommand(TestCommands.NextGroupBy); // by namespace

            // Wait for the test list to be created
            int retry = 5;
            while (Tests == null) {
                Thread.Sleep(250);
                retry--;
                if (retry == 0) {
                    break;
                }
            }
            Assert.IsNotNull(Tests, "Tests list is null");
        }

        /// <summary>
        /// Wait for the node in the tree for the specified Python test.
        /// </summary>
        /// <remarks>
        /// Relies on test explorer to be grouped by namespace.
        /// </remarks>
        public AutomationElement WaitForPythonTest(string moduleFileName, string testClass, string testMethod, string state) {
            var successTest = Tests.WaitForItem(moduleFileName, $"{moduleFileName}::{testClass}::{testMethod}:{state}");
            Assert.IsNotNull(successTest, $"Failed to find test: moduleFileName={moduleFileName}, testClass={testClass}, testMethod={testMethod}, state={state}");
            return successTest;
        }

        /// <summary>
        /// Run all tests and wait for the command to be available again.
        /// </summary>
        public void RunAll(TimeSpan timeout) {
            _app.Dte.ExecuteCommand(TestCommands.RunAllTests);
            Thread.Sleep(100);
            _app.WaitForCommandAvailable(TestCommands.RunAllTests, timeout);
        }

        public class SummaryPane : AutomationWrapper {
            private StackTraceList _stackTraceList;

            public SummaryPane(AutomationElement element)
                : base(element) {
            }

            public void WaitForDetails(string name) {
                var header = FindFirstByNameAndAutomationId(
                    name,
                    "detailPanelHeader"
                );
                Assert.IsNotNull(header, $"Failed to find '{name}' details");
            }

            public StackTraceList StrackTraceList {
                get {
                    if (_stackTraceList == null) {
                        var el = FindByName("Stack Trace Panel");
                        if (el != null) {
                            _stackTraceList = new StackTraceList(el);
                        }
                    }

                    return _stackTraceList;
                }
            }
        }

        public class StackTraceList : ListView {
            public StackTraceList(AutomationElement element) : base(element) { }

            public StackFrameItem GetFrame(int index) {
                var frame = Items[index];
                if (frame != null) {
                    return new StackFrameItem(frame.Element, this);
                }

                return null;
            }
        }

        public class StackFrameItem : ListItem {
            private AutomationWrapper _link;

            public StackFrameItem(AutomationElement element, StackTraceList parent) : base(element, parent) { }

            public AutomationWrapper Hyperlink {
                get {
                    if (_link == null) {
                        var el = FindByAutomationId("stackFrameHyperlink");
                        if (el != null) {
                            _link = new AutomationWrapper(el);
                        }
                    }

                    return _link;
                }
            }

            public string Name => Element.Current.Name;
        }
    }
}

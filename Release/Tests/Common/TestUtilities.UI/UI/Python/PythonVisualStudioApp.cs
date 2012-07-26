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
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using EnvDTE;

namespace TestUtilities.UI.Python {
    class PythonVisualStudioApp : VisualStudioApp {
        private PythonPerfExplorer _perfTreeView;
        private PythonPerfToolBar _perfToolBar;
        public PythonVisualStudioApp(DTE dte)
            : base(dte) {
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void OpenPythonPerformance() {
            Dte.ExecuteCommand("View.PythonPerformanceExplorer");
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void LaunchPythonProfiling() {
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("Analyze.LaunchPythonProfiling"));
        }

        /// <summary>
        /// Provides access to the Python profiling tree view.
        /// </summary>
        public PythonPerfExplorer PythonPerformanceExplorerTreeView {
            get {
                if (_perfTreeView == null) {
                    var element = Element.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "SysTreeView32"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Python Performance"
                            )
                        )
                    );
                    _perfTreeView = new PythonPerfExplorer(element);
                }
                return _perfTreeView;
            }
        }

        /// <summary>
        /// Provides access to the Python profiling tool bar
        /// </summary>
        public PythonPerfToolBar PythonPerformanceExplorerToolBar {
            get {
                if (_perfToolBar == null) {
                    var element = Element.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ClassNameProperty,
                                "ToolBar"
                            ),
                            new PropertyCondition(
                                AutomationElement.NameProperty,
                                "Python Performance"
                            )
                        )
                    );
                    _perfToolBar = new PythonPerfToolBar(element);
                }
                return _perfToolBar;
            }
        }

        internal Document WaitForDocument(string docName) {
            for (int i = 0; i < 100; i++) {
                try {
                    return Dte.Documents.Item(docName);
                } catch {
                    System.Threading.Thread.Sleep(100);
                }
            }
            throw new InvalidOperationException("Document not opened: " + docName);
        }

        /// <summary>
        /// Selects the given source control provider.  Name merely needs to be enough text to disambiguate from other source control providers.
        /// </summary>
        public void SelectDefaultInterpreter(string name) {
            Element.SetFocus();

            // bring up Tools->Options
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("Tools.Options"));

            // wait for it...
            IntPtr dialog = WaitForDialog();

            // go to the tree view which lets us select a set of options...
            var treeView = new TreeView(AutomationElement.FromHandle(dialog).FindFirst(TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "SysTreeView32")
                ));

            treeView.FindItem("Python Tools", "Interpreter Options").SetFocus();

            var defaultInterpreter = new ComboBox(
                AutomationElement.FromHandle(dialog).FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                       new PropertyCondition(
                           AutomationElement.NameProperty,
                           "Default Interpreter:"
                       ),
                       new PropertyCondition(
                           AutomationElement.ControlTypeProperty,
                           ControlType.ComboBox
                       )
                    )
                )
            );

            defaultInterpreter.SelectItem(name);

            Keyboard.PressAndRelease(Key.Enter);
            WaitForDialogDismissed();
        }
    }
}

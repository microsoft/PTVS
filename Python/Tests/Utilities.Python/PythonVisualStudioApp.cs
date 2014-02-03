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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.Python;

namespace TestUtilities.UI.Python {
    class PythonVisualStudioApp : VisualStudioApp {
        private bool _deletePerformanceSessions;
        private PythonPerfExplorer _perfTreeView;
        private PythonPerfToolBar _perfToolBar;
        public PythonVisualStudioApp(DTE dte)
            : base(dte) {
        }

        protected override void Dispose(bool disposing) {
            if (!IsDisposed) {
                try {
                    InteractiveWindow.CloseAll(this);
                } catch (Exception ex) {
                    Console.WriteLine("Error while closing all interactive windows");
                    Console.WriteLine(ex);
                }

                if (_deletePerformanceSessions) {
                    try {
                        dynamic profiling = Dte.GetObject("PythonProfiling");

                        for (dynamic session = profiling.GetSession(1);
                            session != null;
                            session = profiling.GetSession(1)) {
                            profiling.RemoveSession(session, true);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Error while cleaning up profiling sessions");
                        Console.WriteLine(ex);
                    }
                }
            }
            base.Dispose(disposing);
        }

        // Constants for passing to CreateProject
#if DEV10
        // VS 2010 looks up language names as if they are progids, which means
        // passing "Python" may fail, whereas passing the GUID will always
        // succeed.
        public const string TemplateLanguageName = "{888888a0-9f3d-457c-b088-3a5042f75d52}";
#else
        public const string TemplateLanguageName = "Python";
#endif

        public const string PythonApplicationTemplate = "ConsoleAppProject.zip";
        public const string EmptyWebProjectTemplate = "EmptyWebProject.zip";
        public const string BottleWebProjectTemplate = "BottleWebProject.zip";
        public const string FlaskWebProjectTemplate = "FlaskWebProject.zip";
        public const string DjangoWebProjectTemplate = "DjangoWebProject.zip";

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void OpenPythonPerformance() {
            try {
                _deletePerformanceSessions = true;
                Dte.ExecuteCommand("View.PythonPerformanceExplorer");
            } catch {
                // If the package is not loaded yet then the command may not
                // work. Force load the package by opening the Launch dialog.
                var dialog = new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Analyze.LaunchPythonProfiling"));
                dialog.Cancel();
                Dte.ExecuteCommand("View.PythonPerformanceExplorer");
            }
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public PythonPerfTarget LaunchPythonProfiling() {
            _deletePerformanceSessions = true;
            return new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Analyze.LaunchPythonProfiling"));
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

        public InteractiveWindow GetInteractiveWindow(string title) {
            string autoId = GetName(title);
            AutomationElement element = null;
            for (int i = 0; i < 5 && element == null; i++) {
                element = Element.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(
                            AutomationElement.AutomationIdProperty,
                            autoId
                        ),
                        new PropertyCondition(
                            AutomationElement.ClassNameProperty,
                            ""
                        )
                    )
                );
                if (element == null) {
                    System.Threading.Thread.Sleep(100);
                }
            }

            if (element == null) {
                return null;
            }

            return new InteractiveWindow(
                title,
                element.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(
                        AutomationElement.AutomationIdProperty,
                        "WpfTextView"
                    )
                ),
                this
            );

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

        public FormattingOptionsTreeView GetFormattingOptions(string page, out AutomationElement optionsDialog) {
            Element.SetFocus();

            // bring up Tools->Options
            optionsDialog = AutomationElement.FromHandle(OpenDialogWithDteExecuteCommand("Tools.Options"));

            // go to the tree view which lets us select a set of options...
            var treeView = new TreeView(optionsDialog.FindFirst(TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "SysTreeView32")
                ));

            treeView.FindItem("Text Editor", "Python", "Formatting", page).SetFocus();

            for (int i = 0; i < 10; i++) {
                var optionsTree = optionsDialog.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(
                        AutomationElement.AutomationIdProperty,
                        "_optionsTree"
                    )
                );

                if (optionsTree != null) {
                    return new FormattingOptionsTreeView(optionsTree);
                }
                System.Threading.Thread.Sleep(1000);
            }

            AutomationWrapper.DumpElement(optionsDialog);
            Assert.Fail("failed to find _optionsTree page");
            return null;
        }

        /// <summary>
        /// Selects the given interpreter as the default.
        /// </summary>
        public void SelectDefaultInterpreter(string name) {
            Element.SetFocus();

            // bring up Tools->Options
            var dialog = AutomationElement.FromHandle(OpenDialogWithDteExecuteCommand("Tools.Options"));
            try {
                // go to the tree view which lets us select a set of options...
                var treeView = new TreeView(dialog.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(
                        AutomationElement.ClassNameProperty,
                        "SysTreeView32")
                    ));

                treeView.FindItem("Python Tools", "Environment Options").SetFocus();

                var defaultInterpreter = new ComboBox(dialog.FindFirst(
                        TreeScope.Descendants,
                        new AndCondition(
                           new PropertyCondition(
                               AutomationElement.NameProperty,
                               "Default Environment:"
                           ),
                           new PropertyCondition(
                               AutomationElement.ControlTypeProperty,
                               ControlType.ComboBox
                           )
                        )
                    )
                );

                defaultInterpreter.SelectItem(name);
                dialog.AsWrapper().ClickButtonByName("OK");
                WaitForDialogDismissed();
                dialog = null;
            } finally {
                if (dialog != null) {
                    DismissAllDialogs();
                }
            }
        }

        public IInterpreterOptionsService InterpreterService {
            get {
                var model = GetService<IComponentModel>(typeof(SComponentModel));
                return model.GetService<IInterpreterOptionsService>();
            }
        }
    }
}

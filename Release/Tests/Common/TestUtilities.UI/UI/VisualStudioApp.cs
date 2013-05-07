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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace TestUtilities.UI {
    /// <summary>
    /// Provides wrappers for automating the VisualStudio UI.
    /// </summary>
    public class VisualStudioApp : AutomationWrapper {
        private SolutionExplorerTree _solutionExplorerTreeView;
        private ObjectBrowser _objectBrowser;
        private readonly DTE _dte;

        public VisualStudioApp(DTE dte)
            : this(new IntPtr(dte.MainWindow.HWnd)) {
            _dte = dte;
        }

        private VisualStudioApp(IntPtr windowHandle)
            : base(AutomationElement.FromHandle(windowHandle)) {
        }

        public IComponentModel ComponentModel {
            get {
                return (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            }
        }

        /// <summary>
        /// File->Save
        /// </summary>
        public void SaveSelection() {
            Dte.ExecuteCommand("File.SaveSelectedItems");
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void OpenSolutionExplorer() {
            Dte.ExecuteCommand("View.SolutionExplorer");            
        }

        /// <summary>
        /// Opens and activates the object browser window.
        /// </summary>
        public void OpenObjectBrowser()
        {
            Dte.ExecuteCommand("View.ObjectBrowser");
        }

        /// <summary>
        /// Opens and activates the Navigate To window.
        /// </summary>
        public NavigateToDialog OpenNavigateTo() {
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("Edit.NavigateTo"));
            return new NavigateToDialog(WaitForDialog());
        }

        public SaveDialog SaveAs() {
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("File.SaveSelectedItemsAs"));
            return new SaveDialog(WaitForDialog());
        }

        /// <summary>
        /// Gets the specified document.  Filename should be fully qualified filename.
        /// </summary>
        public EditorWindow GetDocument(string filename) {
            Debug.Assert(Path.IsPathRooted(filename));

            string windowName = Path.GetFileName(filename);
            var elem = Element.FindFirst(TreeScope.Descendants, 
                new AndCondition(
                    new PropertyCondition(
                        AutomationElement.ClassNameProperty,
                        "TabItem"
                    ),
                    new PropertyCondition(
                        AutomationElement.NameProperty,
                        windowName
                    )
                )
            );
            if (elem == null) {
                // maybe the file has been modified, try again with a *
                elem = Element.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(
                            AutomationElement.ClassNameProperty,
                            "TabItem"
                        ),
                        new PropertyCondition(
                            AutomationElement.NameProperty,
                            windowName + "*"
                        )
                    )
                );
            }

            elem = elem.FindFirst(TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "WpfTextView"
                )
            );

            return new EditorWindow(filename, elem);
        }

        /// <summary>
        /// Selects the given source control provider.  Name merely needs to be enough text to disambiguate from other source control providers.
        /// </summary>
        public void SelectSourceControlProvider(string providerName) {
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
            
            treeView.FindItem("Source Control", "Plug-in Selection").SetFocus();

            var currentSourceControl = new ComboBox(
                AutomationElement.FromHandle(dialog).FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                       new PropertyCondition(
                           AutomationElement.NameProperty,
                           "Current source control plug-in:"
                       ),
                       new PropertyCondition(
                           AutomationElement.ClassNameProperty,
                           "ComboBox"
                       )
                    )
                )
            );

            currentSourceControl.SelectItem(providerName);

            Keyboard.PressAndRelease(Key.Enter);
            WaitForDialogDismissed();
        }

        public NewProjectDialog FileNewProject() {
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("File.NewProject"));
            IntPtr dialog = WaitForDialog();
            return new NewProjectDialog(AutomationElement.FromHandle(dialog));
        }

        public AttachToProcessDialog OpenDebugAttach() {
            ThreadPool.QueueUserWorkItem(x => Dte.ExecuteCommand("Debug.AttachtoProcess"));
            return new AttachToProcessDialog(WaitForDialog());
        }

        /// <summary>
        /// Waits for a modal dialog to take over VS's main window and returns the HWND for the dialog.
        /// </summary>
        /// <returns></returns>
        public IntPtr WaitForDialog() {
            return WaitForDialogToReplace(Dte.MainWindow.HWnd);
        }

        public ExceptionHelperDialog WaitForException() {
            for (int i = 0; i < 100; i++) {
                var window = FindByName("Exception Helper Indicator Window");
                if (window != null) {
                    var innerPane = window.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(
                            AutomationElement.ControlTypeProperty,
                            ControlType.Pane
                        )
                    );
                    Assert.IsNotNull(innerPane);
                    return new ExceptionHelperDialog(innerPane);
                }
                System.Threading.Thread.Sleep(100);
            }

            Assert.Fail("Failed to find exception helper window");
            return null;
        }


        /// <summary>
        /// Waits for a modal dialog to take over a given window and returns the HWND for the new dialog.
        /// </summary>
        /// <returns>An IntPtr which should be interpreted as an HWND</returns>
        public static IntPtr WaitForDialogToReplace(int originalHwndasInt) {
            IVsUIShell uiShell = VsIdeTestHostContext.ServiceProvider.GetService(typeof(IVsUIShell)) as IVsUIShell;
            IntPtr hwnd;
            uiShell.GetDialogOwnerHwnd(out hwnd);

            for (int i = 0; i < 100 && hwnd.ToInt32() == originalHwndasInt; i++) {
                System.Threading.Thread.Sleep(100);
                uiShell.GetDialogOwnerHwnd(out hwnd);
            }


            if (hwnd.ToInt32() == originalHwndasInt) {
                DumpElement(AutomationElement.FromHandle(hwnd));
            }
            Assert.AreNotEqual(hwnd, IntPtr.Zero);
            Assert.AreNotEqual(hwnd.ToInt32(), originalHwndasInt);
            return hwnd;
        }

        /// <summary>
        /// Waits for a modal dialog to take over VS's main window and returns the HWND for the dialog.
        /// </summary>
        /// <returns></returns>
        public IntPtr WaitForDialogDismissed() {
            IVsUIShell uiShell = VsIdeTestHostContext.ServiceProvider.GetService(typeof(IVsUIShell)) as IVsUIShell;
            IntPtr hwnd;
            uiShell.GetDialogOwnerHwnd(out hwnd);

            for (int i = 0; i < 100 && hwnd.ToInt32() != Dte.MainWindow.HWnd; i++) {
                System.Threading.Thread.Sleep(100);
                uiShell.GetDialogOwnerHwnd(out hwnd);
            }

            Assert.AreEqual(hwnd.ToInt32(), Dte.MainWindow.HWnd);
            return hwnd;
        }

        /// <summary>
        /// Waits for no dialog. If a dialog appears before the timeout expires
        /// then the test fails and the dialog is closed.
        /// </summary>
        public void WaitForNoDialog(TimeSpan timeout) {
            IVsUIShell uiShell = VsIdeTestHostContext.ServiceProvider.GetService(typeof(IVsUIShell)) as IVsUIShell;
            IntPtr hwnd;
            uiShell.GetDialogOwnerHwnd(out hwnd);

            for (int i = 0; i < 100 && hwnd.ToInt32() == Dte.MainWindow.HWnd; i++) {
                System.Threading.Thread.Sleep((int)timeout.TotalMilliseconds / 100);
                uiShell.GetDialogOwnerHwnd(out hwnd);
            }

            if (hwnd != (IntPtr)Dte.MainWindow.HWnd) {
                AutomationWrapper.DumpElement(AutomationElement.FromHandle(hwnd));
                NativeMethods.EndDialog(hwnd, (IntPtr)(int)MessageBoxButton.Cancel);
                Assert.Fail("Dialog appeared - see output for details");
            }
        }

        internal static void CheckMessageBox(params string[] text) {
            CheckMessageBox(MessageBoxButton.Cancel, text);
        }

        internal static void CheckMessageBox(MessageBoxButton button, params string[] text) {
            CheckAndDismissDialog(text, 65535, new IntPtr((int)button));
        }

        /// <summary>
        /// Checks the text of a dialog and dismisses it.
        /// 
        /// dlgField is the field to check the text of.
        /// buttonId is the button to press to dismiss.
        /// </summary>
        private static void CheckAndDismissDialog(string[] text, int dlgField, IntPtr buttonId) {
            IVsUIShell uiShell = VsIdeTestHostContext.ServiceProvider.GetService(typeof(IVsUIShell)) as IVsUIShell;
            IntPtr hwnd;
            uiShell.GetDialogOwnerHwnd(out hwnd);

            for (int i = 0; i < 100 && hwnd.ToInt32() == VsIdeTestHostContext.Dte.MainWindow.HWnd; i++) {
                System.Threading.Thread.Sleep(100);
                uiShell.GetDialogOwnerHwnd(out hwnd);
            }

            Assert.IsTrue(hwnd != IntPtr.Zero, "hwnd is null, We failed to get the dialog");
            Assert.IsTrue(hwnd.ToInt32() != VsIdeTestHostContext.Dte.MainWindow.HWnd, "hwnd is Dte.MainWindow, We failed to get the dialog");
            AutomationWrapper.DumpElement(AutomationElement.FromHandle(hwnd));
            StringBuilder title = new StringBuilder(4096);
            Assert.AreNotEqual(NativeMethods.GetDlgItemText(hwnd, dlgField, title, title.Capacity), (uint)0);

            string t = title.ToString();
            foreach (string expected in text) {
                Assert.IsTrue(t.Contains(expected), string.Format("Did not find '{0}' in '{1}'", expected, t));
            }
            NativeMethods.EndDialog(hwnd, buttonId);
        }

        /// <summary>
        /// Provides access to Visual Studio's solution explorer tree view.
        /// </summary>
        public SolutionExplorerTree SolutionExplorerTreeView {
            get {
                if (_solutionExplorerTreeView == null) {
                    AutomationElement element = null;
                    for (int i = 0; i < 20 && element == null; i++) {
                        element = Element.FindFirst(TreeScope.Descendants,
                            new AndCondition(
                                new PropertyCondition(
                                    AutomationElement.ControlTypeProperty,
                                    ControlType.Pane
                                ),
                                new PropertyCondition(
                                    AutomationElement.NameProperty,
                                    "Solution Explorer"
                                )
                            )
                        );
                        if (element == null) {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    AutomationElement treeElement = null;
                    if (element != null) {
                        for (int i = 0; i < 20 && treeElement == null; i++) {
                            treeElement = element.FindFirst(TreeScope.Descendants,
                                new PropertyCondition(
                                    AutomationElement.ControlTypeProperty,
                                    ControlType.Tree
                                )
                            );
                            if (treeElement == null) {
                                System.Threading.Thread.Sleep(500);
                            }
                        }
                    }
                    _solutionExplorerTreeView = new SolutionExplorerTree(treeElement);
                }
                return _solutionExplorerTreeView;
            }
        }

        /// <summary>
        /// Provides access to Visual Studio's object browser.
        /// </summary>
        public ObjectBrowser ObjectBrowser
        {
            get
            {
                if (_objectBrowser == null)
                {
                    AutomationElement element = null;
                    for (int i = 0; i < 10 && element == null; i++)
                    {
                        element = Element.FindFirst(TreeScope.Descendants,
                            new AndCondition(
                                new PropertyCondition(
                                    AutomationElement.ClassNameProperty,
                                    "ViewPresenter"
                                ),
                                new PropertyCondition(
                                    AutomationElement.NameProperty,
                                    "Object Browser"
                                )
                            )
                        );
                        if (element == null)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    _objectBrowser = new ObjectBrowser(element);
                }
                return _objectBrowser;
            }
        }
        
        /// <summary>
        /// Produces a name which is compatible with x:Name requirements (starts with a letter/underscore, contains
        /// only letter, numbers, or underscores).
        /// </summary>
        public static string GetName(string title) {
            if (title.Length == 0) {
                return "InteractiveWindowHost";
            }

            StringBuilder res = new StringBuilder();
            if (!Char.IsLetter(title[0])) {
                res.Append('_');
            }

            foreach (char c in title) {
                if (Char.IsLetter(c) || Char.IsDigit(c) || c == '_') {
                    res.Append(c);
                }
            }
            res.Append("Host");
            return res.ToString();
        }

        public void MoveCurrentFileToProject(string projectName) {
            ThreadPool.QueueUserWorkItem((x) => Dte.ExecuteCommand("file.ProjectPickerMoveInto"));
            IntPtr dialog = WaitForDialog();

            var chooseDialog = new ChooseLocationDialog(dialog);
            chooseDialog.FindProject(projectName);
            chooseDialog.ClickOK();

            WaitForDialogDismissed();
        }

        public DTE Dte {
            get {
                return _dte;
            }
        }

        internal void OpenProject(string path) {
            ThreadPool.QueueUserWorkItem((x) => Dte.ExecuteCommand("File.OpenProject"));
            
            var dialog = new OpenProjectDialog(WaitForDialog());
            dialog.ProjectName = path;
            dialog.Open();

            WaitForDialogDismissed();
        }

        internal void WaitForMode(dbgDebugMode mode) {
            for (int i = 0; i < 300 && Dte.Debugger.CurrentMode != mode; i++) {
                System.Threading.Thread.Sleep(100);
            }

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, mode);
        }

    }
}

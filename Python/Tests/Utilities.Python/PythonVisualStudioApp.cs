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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using EnvDTE;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;
using TestUtilities.Python;
using Process = System.Diagnostics.Process;

namespace TestUtilities.UI.Python {
    class PythonVisualStudioApp : VisualStudioApp {
        private bool _deletePerformanceSessions;
        private PythonPerfExplorer _perfTreeView;
        private PythonPerfToolBar _perfToolBar;
        public PythonVisualStudioApp(DTE dte = null)
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
        private const string _templateLanguageName = "Python";
        public static string TemplateLanguageName {
            get {
#if DEV10
                // VS 2010 looks up language names as if they are progids, which means
                // passing "Python" may fail, whereas passing the GUID will always
                // succeed.
                using (var progid = Registry.ClassesRoot.OpenSubKey(_templateLanguageName)) {
                    Assert.IsNull(progid, "Python is a registered progid. Templates cannot be created in VS 2010");
                }
#endif
                return _templateLanguageName;
            }
        }

        public const string PythonApplicationTemplate = "ConsoleAppProject.zip";
        public const string EmptyWebProjectTemplate = "EmptyWebProject.zip";
        public const string BottleWebProjectTemplate = "WebProjectBottle.zip";
        public const string FlaskWebProjectTemplate = "WebProjectFlask.zip";
        public const string DjangoWebProjectTemplate = "DjangoProject.zip";
        public const string WorkerRoleProjectTemplate = "WorkerRoleProject.zip";
        
        public const string EmptyFileTemplate = "EmptyPyFile.zip";
        public const string WebRoleSupportTemplate = "AzureCSWebRole.zip";
        public const string WorkerRoleSupportTemplate = "AzureCSWorkerRole.zip";

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public void OpenPythonPerformance() {
            try {
                _deletePerformanceSessions = true;
                Dte.ExecuteCommand("Python.PerformanceExplorer");
            } catch {
                // If the package is not loaded yet then the command may not
                // work. Force load the package by opening the Launch dialog.
                using (var dialog = new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Python.LaunchProfiling"))) {
                }
                Dte.ExecuteCommand("Python.PerformanceExplorer");
            }
        }

        /// <summary>
        /// Opens and activates the solution explorer window.
        /// </summary>
        public PythonPerfTarget LaunchPythonProfiling() {
            _deletePerformanceSessions = true;
            return new PythonPerfTarget(OpenDialogWithDteExecuteCommand("Python.LaunchProfiling"));
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
                DumpVS();
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

        /// <summary>
        /// Selects the given interpreter as the default.
        /// </summary>
        /// <remarks>
        /// This method should always be called as a using block.
        /// </remarks>
        public DefaultInterpreterSetter SelectDefaultInterpreter(PythonVersion python) {
            return new DefaultInterpreterSetter(
                InterpreterService.FindInterpreter(python.Id, python.Version.ToVersion())
            );
        }

        public DefaultInterpreterSetter SelectDefaultInterpreter(PythonVersion interp, string installPackages) {
            interp.AssertInstalled();
            if (interp.IsIronPython && !string.IsNullOrEmpty(installPackages)) {
                Assert.Inconclusive("Package installation not supported on IronPython");
            }

            var interpreterService = InterpreterService;
            var factory = interpreterService.FindInterpreter(interp.Id, interp.Configuration.Version);
            var defaultInterpreterSetter = new DefaultInterpreterSetter(factory);

            try {
                if (!string.IsNullOrEmpty(installPackages)) {
                    Pip.InstallPip(factory, false).Wait();
                    foreach (var package in installPackages.Split(' ', ',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s))) {
                        Pip.Install(factory, package, false).Wait();
                    }
                }

                var result = defaultInterpreterSetter;
                defaultInterpreterSetter = null;
                return result;
            } finally {
                if (defaultInterpreterSetter != null) {
                    defaultInterpreterSetter.Dispose();
                }
            }
        }


        public IInterpreterOptionsService InterpreterService {
            get {
                var model = GetService<IComponentModel>(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();
                Assert.IsNotNull(service, "Unable to get InterpreterOptionsService");
                return service;
            }
        }

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName) {
            string dummy;
            return CreateVirtualEnvironment(project, out envName, out dummy);
        }

        public TreeNode CreateVirtualEnvironment(EnvDTE.Project project, out string envName, out string envPath) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(
                project,
                SR.GetString(SR.Environments)
            );
            environmentsNode.Select();

            var alreadyRunning = new HashSet<int>(Process.GetProcessesByName("python").Select(p => p.Id));

            using (var createVenv = AutomationDialog.FromDte(this, "Python.AddVirtualEnvironment")) {
                envPath = new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).GetValue();
                var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();

                envName = string.Format("{0} ({1})", envPath, baseInterp);

                Console.WriteLine("Expecting environment named: {0}", envName);

                // Force a wait for the view to be updated.
                var wnd = (DialogWindowVersioningWorkaround)HwndSource.FromHwnd(
                    new IntPtr(createVenv.Element.Current.NativeWindowHandle)
                ).RootVisual;
                wnd.Dispatcher.Invoke(() => {
                    var view = (AddVirtualEnvironmentView)wnd.DataContext;
                    return view.UpdateInterpreter(view.BaseInterpreter);
                }).Wait();

                createVenv.ClickButtonByAutomationId("Create");
                createVenv.ClickButtonAndClose("Close", nameIsAutomationId: true);
            }

            var nowRunning = Process.GetProcessesByName("python").Where(p => !alreadyRunning.Contains(p.Id)).ToArray();
            foreach (var p in nowRunning) {
                if (p.HasExited) {
                    continue;
                }
                try {
                    p.WaitForExit(30000);
                } catch (Win32Exception) {
                }
            }
            
            return OpenSolutionExplorer().WaitForChildOfProject(
                project,
                SR.GetString(SR.Environments),
                envName
            );
        }

        public TreeNode AddExistingVirtualEnvironment(EnvDTE.Project project, string envPath, out string envName) {
            var environmentsNode = OpenSolutionExplorer().FindChildOfProject(
                project,
                SR.GetString(SR.Environments)
            );
            environmentsNode.Select();

            using (var createVenv = AutomationDialog.FromDte(this, "Python.AddVirtualEnvironment")) {
                new TextBox(createVenv.FindByAutomationId("VirtualEnvPath")).SetValue(envPath);
                var baseInterp = new ComboBox(createVenv.FindByAutomationId("BaseInterpreter")).GetSelectedItemName();

                envName = string.Format("{0} ({1})", Path.GetFileName(envPath), baseInterp);

                Console.WriteLine("Expecting environment named: {0}", envName);

                createVenv.ClickButtonAndClose("Add", nameIsAutomationId: true);
            }

            return OpenSolutionExplorer().WaitForChildOfProject(
                project,
                SR.GetString(SR.Environments),
                envName
            );
        }

        public IPythonOptions Options {
            get {
                return (IPythonOptions)Dte.GetObject("VsPython");
            }
        }

    }
}

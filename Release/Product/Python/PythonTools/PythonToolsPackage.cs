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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>    
    [PackageRegistration(UseManagedResourcesOnly = true)]       // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is a package.
#pragma warning disable 0436    // InternalsVisibleTo in debugger causes us to conflict on AssemblyVersionInfo
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version,        // This attribute is used to register the informations needed to show the this package in the Help/About dialog of Visual Studio.
        IconResourceID = 400)]
#pragma warning restore 0436
    [ProvideMenuResource(1000, 1)]                              // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideAutoLoad(CommonConstants.UIContextNoSolution)]
    [ProvideAutoLoad(CommonConstants.UIContextSolutionExists)]
    [Description("Python Tools Package")]
    [ProvideAutomationObject("VsPython")]
    [ProvideLanguageEditorOptionPage(typeof(PythonAdvancedEditorOptionsPage), PythonConstants.LanguageName, "", "Advanced", "114")]
    [ProvideOptionPage(typeof(PythonInterpreterOptionsPage), "Python Tools", "Interpreters", 115, 116, true)]
    [ProvideOptionPage(typeof(PythonInteractiveOptionsPage), "Python Tools", "Interactive Windows", 115, 117, true)]
    [ProvideOptionPage(typeof(PythonDebugInteractiveOptionsPage), "Python Tools", "Debug Interactive Window", 115, 119, true)]
    [ProvideOptionPage(typeof(PythonAdvancedOptionsPage), "Python Tools", "Advanced", 115, 118, true)]
    [Guid(GuidList.guidPythonToolsPkgString)]              // our packages GUID        
    [ProvideLanguageService(typeof(PythonLanguageInfo), PythonConstants.LanguageName, 106, RequestStockColors = true, ShowSmartIndent = true, ShowCompletion = true, DefaultToInsertSpaces = true, HideAdvancedMembersByDefault = false, EnableAdvancedMembersOption = true, ShowDropDownOptions = true)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.FileExtension)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.WindowsFileExtension)]
    [ProvideDebugEngine("Python Debugging", typeof(AD7ProgramProvider), typeof(AD7Engine), AD7Engine.DebugEngineId)]
    [ProvideDebugLanguage("Python", "{DA3C7D59-F9E4-4697-BEE7-3A0703AF6BFF}", AD7Engine.DebugEngineId)]
    [ProvidePythonExecutionModeAttribute(ExecutionMode.StandardModeId, "Standard", "Standard")]
    [ProvidePythonExecutionModeAttribute("{91BB0245-B2A9-47BF-8D76-DD428C6D8974}", "IPython", "visualstudio_ipython_repl.IPythonBackend", false)]
    [ProvidePythonExecutionModeAttribute("{3E390328-A806-4250-ACAD-97B5B37076E2}", "IPython w/o PyLab", "visualstudio_ipython_repl.IPythonBackendWithoutPyLab", false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ArithmeticError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.AssertionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.AttributeError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BaseException")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BufferError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BytesWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.DeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.EOFError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.EnvironmentError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.Exception")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.FloatingPointError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.FutureWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.GeneratorExit", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ImportError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ImportWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IndentationError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IndexError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.KeyError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.KeyboardInterrupt")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.LookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.MemoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.NameError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.NotImplementedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.OSError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.OverflowError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.PendingDeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ReferenceError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.RuntimeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.RuntimeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.StandardError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.StopIteration", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SyntaxError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SyntaxWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SystemError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SystemExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.TabError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.TypeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnboundLocalError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeDecodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeEncodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeTranslateError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UserWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ValueError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.Warning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.WindowsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ZeroDivisionError")]
    [ProvideComponentPickerPropertyPage(typeof(PythonToolsPackage), typeof(WebPiComponentPickerControl), "WebPi", DefaultPageNameValue="#4000")]
    public sealed class PythonToolsPackage : CommonPackage, IVsComponentSelectorProvider {
        private LanguagePreferences _langPrefs;
        public static PythonToolsPackage Instance;
        private VsProjectAnalyzer _analyzer;
        private static Dictionary<Command, MenuCommand> _commands = new Dictionary<Command,MenuCommand>();
        private PythonAutomation _autoObject = new PythonAutomation();
        private IContentType _contentType;
        private PackageContainer _packageContainer;
        internal static Guid _noInterpretersFactoryGuid = new Guid("{15CEBB59-1008-4305-97A9-CF5E2CB04711}");
        private static List<EventHandler> _earlyHandlers = new List<EventHandler>();
        private UpdateSolutionEventsListener _solutionEventListener;
        internal static SolutionAdvisor _solutionAdvisor;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public PythonToolsPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
            Instance = this;

            // hook up any event handlers that were registered before we were sited.
            foreach (var earlyHandler in _earlyHandlers) {
                InterpretersChanged += earlyHandler;
            }
            _earlyHandlers.Clear();

            if (IsIpyToolsInstalled()) {
                MessageBox.Show(
                    @"WARNING: Both Python Tools for Visual Studio and IronPython Tools are installed.

Only one extension can handle Python source files and having both installed will usually cause both to be broken.

You should uninstall IronPython 2.7 and re-install it with the ""Tools for Visual Studio"" option unchecked.",
                    "Python Tools for Visual Studio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        internal static bool IsIpyToolsInstalled() {
            // the component guid which IpyTools is installed under from IronPython 2.7
            const string ipyToolsComponentGuid = "{2DF41B37-FAEF-4FD8-A2F5-46B57FF9E951}";  

            // Check if the IpyTools component is known...
            StringBuilder productBuffer = new StringBuilder(39);
            if (NativeMethods.MsiGetProductCode(ipyToolsComponentGuid, productBuffer) == 0) {
                // If it is then make sure that it's installed locally...
                StringBuilder buffer = new StringBuilder(1024);
                uint charsReceived = (uint)buffer.Capacity;
                var res = NativeMethods.MsiGetComponentPath(productBuffer.ToString(), ipyToolsComponentGuid, buffer, ref charsReceived);
                switch (res) {
                    case NativeMethods.MsiInstallState.Source:
                    case NativeMethods.MsiInstallState.Local:
                        return true;
                }
            }
            return false;
        }

        internal static void NavigateTo(string filename, Guid docViewGuidType, int line, int col) {
            IVsTextView viewAdapter;
            IVsWindowFrame pWindowFrame;
            OpenDocument(filename, out viewAdapter, out pWindowFrame);
            
            ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

            // Set the cursor at the beginning of the declaration.            
            ErrorHandler.ThrowOnFailure(viewAdapter.SetCaretPos(line, col));
            // Make sure that the text is visible.
            viewAdapter.CenterLines(line, 1);
        }

        internal static void NavigateTo(string filename, Guid docViewGuidType, int pos) {
            IVsTextView viewAdapter;
            IVsWindowFrame pWindowFrame;
            OpenDocument(filename, out viewAdapter, out pWindowFrame);

            ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

            // Set the cursor at the beginning of the declaration.          
            int line, col;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetLineAndColumn(pos, out line, out col));
            ErrorHandler.ThrowOnFailure(viewAdapter.SetCaretPos(line, col));
            // Make sure that the text is visible.
            viewAdapter.CenterLines(line, 1);
        }

        internal static ITextBuffer GetBufferForDocument(string filename) {
            IVsTextView viewAdapter;
            IVsWindowFrame frame;
            OpenDocument(filename, out viewAdapter, out frame);

            IVsTextLines lines;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetBuffer(out lines));
            
            var model = Instance.GetService(typeof(SComponentModel)) as IComponentModel;
            var adapter = model.GetService<IVsEditorAdaptersFactoryService>();

            return adapter.GetDocumentBuffer(lines);            
        }

        private static void OpenDocument(string filename, out IVsTextView viewAdapter, out IVsWindowFrame pWindowFrame) {
            IVsTextManager textMgr = (IVsTextManager)Instance.GetService(typeof(SVsTextManager));

            IVsUIShellOpenDocument uiShellOpenDocument = Instance.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            IVsUIHierarchy hierarchy;
            uint itemid;


            VsShellUtilities.OpenDocument(
                Instance,
                filename,
                Guid.Empty,
                out hierarchy,
                out itemid,
                out pWindowFrame,
                out viewAdapter);
        }

        protected override object GetAutomationObject(string name) {
            if (name == "VsPython") {
                return _autoObject;
            }

            return base.GetAutomationObject(name);
        }

        public override bool IsRecognizedFile(string filename) {
            return PythonProjectNode.IsPythonFile(filename);
        }

        public override Type GetLibraryManagerType() {
            return typeof(IPythonLibraryManager);
        }

        public string InteractiveOptions {
            get {
                // FIXME
                return "";
            }
        }

        public PythonAdvancedOptionsPage OptionsPage {
            get {
                return (PythonAdvancedOptionsPage)GetDialogPage(typeof(PythonAdvancedOptionsPage));
            }
        }

        public PythonAdvancedEditorOptionsPage AdvancedEditorOptionsPage {
            get {
                return (PythonAdvancedEditorOptionsPage)GetDialogPage(typeof(PythonAdvancedEditorOptionsPage));
            }
        }

        internal PythonInterpreterOptionsPage InterpreterOptionsPage {
            get {
                return (PythonInterpreterOptionsPage)GetDialogPage(typeof(PythonInterpreterOptionsPage));
            }
        }

        internal PythonInteractiveOptionsPage InteractiveOptionsPage {
            get {
                return (PythonInteractiveOptionsPage)GetDialogPage(typeof(PythonInteractiveOptionsPage));
            }
        }

        internal PythonDebugInteractiveOptionsPage InteractiveDebugOptionsPage {
            get {
                return (PythonDebugInteractiveOptionsPage)GetDialogPage(typeof(PythonDebugInteractiveOptionsPage));
            }
        }

        /// <summary>
        /// Event is fired when the list of configured interpreters is changed.
        /// 
        /// New in 1.1.
        /// </summary>
        public static event EventHandler InterpretersChanged {
            add {
                if (Instance == null) {
                    _earlyHandlers.Add(value);
                } else {
                    Instance.InterpreterOptionsPage.InterpretersChanged += value;
                }
            }
            remove {
                if (Instance == null) {
                    _earlyHandlers.Remove(value);
                } else {
                    Instance.InterpreterOptionsPage.InterpretersChanged -= value;
                }
            }
        }

        /// <summary>
        /// The analyzer which is used for loose files.
        /// </summary>
        internal VsProjectAnalyzer DefaultAnalyzer {
            get {
                if (_analyzer == null) {
                    _analyzer = CreateAnalyzer();
                }
                return _analyzer;
            }
        }

        internal void RecreateAnalyzer() {
            if (_analyzer != null) {
                _analyzer.Dispose();
            }
            _analyzer = CreateAnalyzer();
        }

        private VsProjectAnalyzer CreateAnalyzer() {
            var model = GetService(typeof(SComponentModel)) as IComponentModel;

            var defaultFactory = model.GetAllPythonInterpreterFactories().GetDefaultInterpreter();
            EnsureCompletionDb(defaultFactory);
            return new VsProjectAnalyzer(defaultFactory.CreateInterpreter(), defaultFactory, model.GetAllPythonInterpreterFactories(), model.GetService<IErrorProviderFactory>());
        }

        /// <summary>
        /// Asks the interpreter to auto-generate it's completion database if it doesn't already exist and the user
        /// hasn't disabled this option.
        /// </summary>
        internal static void EnsureCompletionDb(IPythonInterpreterFactory fact) {
            if (PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary) {
                IInterpreterWithCompletionDatabase interpWithDb = fact as IInterpreterWithCompletionDatabase;
                if (interpWithDb != null) {
                    interpWithDb.AutoGenerateCompletionDatabase();
                }
            }
        }

        private void UpdateDefaultAnalyzer(object sender, EventArgs args) {
            // no need to update if analyzer isn't created yet.
            if (_analyzer != null) {
                var analyzer = CreateAnalyzer();

                if (_analyzer != null) {
                    analyzer.SwitchAnalyzers(_analyzer);
                }
            }
        }

        internal override LibraryManager CreateLibraryManager(CommonPackage package) {
            return new PythonLibraryManager((PythonToolsPackage)package);
        }

        public IVsSolution Solution {
            get {
                return GetService(typeof(SVsSolution)) as IVsSolution;
            }
        }

        internal static new RegistryKey UserRegistryRoot {
            get {
                if (Instance != null) {
                    return ((CommonPackage)Instance).UserRegistryRoot;
                }

#if DEV11
                return Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }

        internal static new RegistryKey ApplicationRegistryRoot {
            get {
                if (Instance != null) {
                    return ((CommonPackage)Instance).ApplicationRegistryRoot;
                }


#if DEV11
                return Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // register our language service so that we can support features like the navigation bar
            var langService = new PythonLanguageInfo(this);
            ((IServiceContainer)this).AddService(langService.GetType(), langService, true);

            var solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            _solutionAdvisor = new SolutionAdvisor(solution);

            IVsTextManager textMgr = (IVsTextManager)Instance.GetService(typeof(SVsTextManager));
            var langPrefs = new LANGPREFERENCES[1];
            langPrefs[0].guidLang = typeof(PythonLanguageInfo).GUID;
            ErrorHandler.ThrowOnFailure(textMgr.GetUserPreferences(null, null, langPrefs, null));
            _langPrefs = new LanguagePreferences(langPrefs[0]);

            Guid guid = typeof(IVsTextManagerEvents2).GUID;
            IConnectionPoint connectionPoint;
            ((IConnectionPointContainer)textMgr).FindConnectionPoint(ref guid, out connectionPoint);
            uint cookie;
            connectionPoint.Advise(_langPrefs, out cookie);

            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            
            // Add our command handlers for menu (commands must exist in the .vsct file)
            RegisterCommands(new Command[] { 
                new OpenDebugReplCommand(), 
                new ExecuteInReplCommand(), 
                new SendToReplCommand(), 
                new FillParagraphCommand(), 
                new SendToDefiningModuleCommand(), 
                new DiagnosticsCommand(),
                new RemoveImportsCommand(),
                new RemoveImportsCurrentScopeCommand()
            });

            RegisterCommands(GetReplCommands());

            InterpreterOptionsPage.InterpretersChanged += OnInterpretersChanged;
            InterpreterOptionsPage.DefaultInterpreterChanged += UpdateDefaultAnalyzer;

            _solutionEventListener = new UpdateSolutionEventsListener(PythonToolsPackage.Instance);
        }

        internal SolutionAdvisor SolutionAdvisor {
            get {
                return _solutionAdvisor;
            }
        }

        internal UpdateSolutionEventsListener EventListener {
            get {
                return _solutionEventListener;
            }
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            RefreshReplCommands();
        }

        private void RegisterCommands(IEnumerable<Command> commands) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                foreach (var command in commands) {
                    var beforeQueryStatus = command.BeforeQueryStatus;
                    CommandID toolwndCommandID = new CommandID(GuidList.guidPythonToolsCmdSet, command.CommandId);
                    if (beforeQueryStatus == null) {
                        MenuCommand menuToolWin = new MenuCommand(command.DoCommand, toolwndCommandID);                        
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    } else {
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);                        
                        menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        internal void RefreshReplCommands() {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            List<OpenReplCommand> replCommands = new List<OpenReplCommand>();
            foreach (var keyValue in _commands) {
                var command = keyValue.Key;
                OpenReplCommand openRepl = command as OpenReplCommand;
                if (openRepl != null) {
                    replCommands.Add(openRepl);

                    mcs.RemoveCommand(keyValue.Value);
                }
            }

            foreach (var command in replCommands) {
                _commands.Remove(command);
            }

            RegisterCommands(GetReplCommands());
        }

        private List<OpenReplCommand> GetReplCommands() {
            var factories = ComponentModel.GetAllPythonInterpreterFactories();
            var defaultFactory = factories.GetDefaultInterpreter();
            // sort so default always comes first, and otherwise in sorted order
            Array.Sort(factories, (x, y) => {
                if (x == y) {
                    return 0;
                } else if (x == defaultFactory) {
                    return -1;
                } else if (y == defaultFactory) {
                    return 1;
                } else {
                    return String.Compare(x.GetInterpreterDisplay(), y.GetInterpreterDisplay());
                }
            });

            var replCommands = new List<OpenReplCommand>();
            for (int i = 0; i < (PkgCmdIDList.cmdidReplWindowF - PkgCmdIDList.cmdidReplWindow) && i < factories.Length; i++) {
                var factory = factories[i];

                var cmd = new OpenReplCommand((int)PkgCmdIDList.cmdidReplWindow + i, factory);
                replCommands.Add(cmd);
            }
            return replCommands;
        }

        internal static bool TryGetStartupFileAndDirectory(out string filename, out string dir, out VsProjectAnalyzer analyzer) {
            var startupProject = GetStartupProject();
            if (startupProject != null) {
                filename = startupProject.GetStartupFile();
                dir = startupProject.GetWorkingDirectory();
                analyzer = ((PythonProjectNode)startupProject).GetAnalyzer();
            } else {
                var textView = CommonPackage.GetActiveTextView();
                if (textView == null) {
                    filename = null;
                    dir = null;
                    analyzer = null;
                    return false;
                }
                filename = textView.GetFilePath();
                analyzer = textView.GetAnalyzer();
                dir = Path.GetDirectoryName(filename);
            }
            return true;
        }

        public bool AutoListMembers {
            get {
                return _langPrefs.AutoListMembers;
            }
        }

        internal LanguagePreferences LangPrefs {
            get {
                return _langPrefs;
            }
        }

        public EnvDTE.DTE DTE {
            get {
                return (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            }
        }

        public IContentType ContentType {
            get {
                if (_contentType == null) {
                    _contentType = ComponentModel.GetService<IContentTypeRegistryService>().GetContentType(PythonCoreConstants.ContentType);
                }
                return _contentType;
            }
        }

        internal static Dictionary<Command, MenuCommand> Commands {
            get {
                return _commands;
            }
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        internal static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "PyDebugAttach.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = PythonToolsPackage.ApplicationRegistryRoot) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.5");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        #region IVsComponentSelectorProvider Members

        public int GetComponentSelectorPage(ref Guid rguidPage, VSPROPSHEETPAGE[] ppage) {
            if (rguidPage == typeof(WebPiComponentPickerControl).GUID) {
                var page = new VSPROPSHEETPAGE();
                page.dwSize = (uint)Marshal.SizeOf(typeof(VSPROPSHEETPAGE));
                var pickerPage = new WebPiComponentPickerControl();
                if (_packageContainer == null) {
                    _packageContainer = new PackageContainer(this);
                }
                _packageContainer.Add(pickerPage);
                //IWin32Window window = pickerPage;
                page.hwndDlg = pickerPage.Handle;
                ppage[0] = page;
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        /// <devdoc>
        ///     This class derives from container to provide a service provider
        ///     connection to the package.
        /// </devdoc>
        private sealed class PackageContainer : Container {
            private IUIService _uis;
            private AmbientProperties _ambientProperties;

            private System.IServiceProvider _provider;

            /// <devdoc>
            ///     Creates a new container using the given service provider.
            /// </devdoc>
            internal PackageContainer(System.IServiceProvider provider) {
                _provider = provider;
            }

            /// <devdoc>
            ///     Override to GetService so we can route requests
            ///     to the package's service provider.
            /// </devdoc>
            protected override object GetService(Type serviceType) {
                if (serviceType == null) {
                    throw new ArgumentNullException("serviceType");
                }
                if (_provider != null) {
                    if (serviceType.IsEquivalentTo(typeof(AmbientProperties))) {
                        if (_uis == null) {
                            _uis = (IUIService)_provider.GetService(typeof(IUIService));
                        }
                        if (_ambientProperties == null) {
                            _ambientProperties = new AmbientProperties();
                        }
                        if (_uis != null) {
                            // update the _ambientProperties in case the styles have changed
                            // since last time.
                            _ambientProperties.Font = (Font)_uis.Styles["DialogFont"];
                        }
                        return _ambientProperties;
                    }
                    object service = _provider.GetService(serviceType);

                    if (service != null) {
                        return service;
                    }
                }
                return base.GetService(serviceType);
            }
        }

        #endregion
    }
}

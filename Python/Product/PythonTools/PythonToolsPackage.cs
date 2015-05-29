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
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Editor;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;
using SR = Microsoft.PythonTools.Project.SR;
#if DEV14_OR_LATER
using IReplWindow = Microsoft.VisualStudio.InteractiveWindow.IInteractiveWindow;
using IReplWindowProvider = Microsoft.VisualStudio.InteractiveWindow.Shell.IVsInteractiveWindowFactory;
using IReplEvaluatorProvider = Microsoft.PythonTools.Repl.IInteractiveEvaluatorProvider;
#endif

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
    // This attribute is used to register the informations needed to show the this package in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version, IconResourceID = 400)]

    // This attribute is needed to let the shell know that this package exposes some menus.
#if DEV14_OR_LATER
    [ProvideMenuResource(1000, 1, IconMappingFilename = "PythonTools_iconmap.csv")]
#else
    [ProvideMenuResource(1000, 1)]
#endif
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids.NoSolution)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids.SolutionExists)]
    [Description("Python Tools Package")]
    [ProvideAutomationObject("VsPython")]
    [ProvideLanguageEditorOptionPage(typeof(PythonAdvancedEditorOptionsPage), PythonConstants.LanguageName, "", "Advanced", "113")]
    [ProvideLanguageEditorOptionPage(typeof(PythonFormattingGeneralOptionsPage), PythonConstants.LanguageName, "Formatting", "General", "120")]
    //[ProvideLanguageEditorOptionPage(typeof(PythonFormattingNewLinesOptionsPage), PythonConstants.LanguageName, "Formatting", "New Lines", "121")]
    [ProvideLanguageEditorOptionPage(typeof(PythonFormattingSpacingOptionsPage), PythonConstants.LanguageName, "Formatting", "Spacing", "122")]
    [ProvideLanguageEditorOptionPage(typeof(PythonFormattingStatementsOptionsPage), PythonConstants.LanguageName, "Formatting", "Statements", "123")]
    [ProvideLanguageEditorOptionPage(typeof(PythonFormattingWrappingOptionsPage), PythonConstants.LanguageName, "Formatting", "Wrapping", "124")]
    // Continue to provide this options page as "Interpreters" for DTE access
    // and because it matches the registry store. The localized string should
    // read "Environments".
    [ProvideOptionPage(typeof(PythonInterpreterOptionsPage), "Python Tools", "Interpreters", 115, 116, true)]
    [ProvideOptionPage(typeof(PythonInteractiveOptionsPage), "Python Tools", "Interactive Windows", 115, 117, true)]
    [ProvideOptionPage(typeof(PythonDebugInteractiveOptionsPage), "Python Tools", "Debug Interactive Window", 115, 119, true)]
    [ProvideOptionPage(typeof(PythonGeneralOptionsPage), "Python Tools", "General", 115, 120, true)]
    [ProvideOptionPage(typeof(PythonDebuggingOptionsPage), "Python Tools", "Debugging", 115, 125, true)]
    [Guid(GuidList.guidPythonToolsPkgString)]              // our packages GUID        
    [ProvideLanguageService(typeof(PythonLanguageInfo), PythonConstants.LanguageName, 106, RequestStockColors = true, ShowSmartIndent = true, ShowCompletion = true, DefaultToInsertSpaces = true, HideAdvancedMembersByDefault = true, EnableAdvancedMembersOption = true, ShowDropDownOptions = true)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.FileExtension)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.WindowsFileExtension)]
    [ProvideDebugEngine(AD7Engine.DebugEngineName, typeof(AD7ProgramProvider), typeof(AD7Engine), AD7Engine.DebugEngineId,  hitCountBp: true)]
    [ProvideDebugLanguage("Python", "{DA3C7D59-F9E4-4697-BEE7-3A0703AF6BFF}", PythonExpressionEvaluatorGuid, AD7Engine.DebugEngineId)]
    [ProvideDebugPortSupplier("Python remote (ptvsd)", typeof(PythonRemoteDebugPortSupplier), PythonRemoteDebugPortSupplier.PortSupplierId, typeof(PythonRemoteDebugPortPicker))]
    [ProvideDebugPortPicker(typeof(PythonRemoteDebugPortPicker))]
    [ProvidePythonExecutionMode(ExecutionMode.StandardModeId, "Standard", "Standard")]
    [ProvidePythonExecutionMode("{91BB0245-B2A9-47BF-8D76-DD428C6D8974}", "IPython", "visualstudio_ipython_repl.IPythonBackend", supportsMultipleScopes: false, supportsMultipleCompleteStatementInputs: true)]
    [ProvidePythonExecutionMode("{3E390328-A806-4250-ACAD-97B5B37076E2}", "IPython w/o PyLab", "visualstudio_ipython_repl.IPythonBackendWithoutPyLab", supportsMultipleScopes: false, supportsMultipleCompleteStatementInputs: true)]
    #region Exception List
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ArithmeticError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "AssertionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "AttributeError", BreakByDefault = false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "BaseException")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "BufferError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "BytesWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "DeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "EOFError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "EnvironmentError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Exception")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "FloatingPointError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "FutureWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "GeneratorExit", BreakByDefault = false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "IOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ImportError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ImportWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "IndentationError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "IndexError", BreakByDefault = false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "KeyError", BreakByDefault = false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "KeyboardInterrupt")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "LookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "MemoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "NameError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "NotImplementedError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "BlockingIOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ChildProcessError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ConnectionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ConnectionError", "BrokenPipeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ConnectionError", "ConnectionAbortedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ConnectionError", "ConnectionRefusedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ConnectionError", "ConnectionResetError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "FileExistsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "FileNotFoundError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "InterruptedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "IsADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "NotADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "PermissionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "ProcessLookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OSError", "TimeoutError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "OverflowError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "PendingDeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ReferenceError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "RuntimeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "RuntimeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "StandardError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "StopIteration", BreakByDefault = false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "SyntaxError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "SyntaxWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "SystemError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "SystemExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "TabError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "TypeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnboundLocalError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnicodeDecodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnicodeEncodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnicodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnicodeTranslateError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UnicodeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "UserWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ValueError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Warning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "WindowsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "ZeroDivisionError")]
    #endregion
    [ProvideComponentPickerPropertyPage(typeof(PythonToolsPackage), typeof(WebPiComponentPickerControl), "WebPi", DefaultPageNameValue = "#4000")]
    [ProvideToolWindow(typeof(InterpreterListToolWindow), Style = VsDockStyle.Linked, Window = ToolWindowGuids80.SolutionExplorer)]
    [ProvidePythonInterpreterFactoryProvider(CPythonInterpreterFactoryConstants.Id32, typeof(CPythonInterpreterFactoryConstants))]
    [ProvidePythonInterpreterFactoryProvider(CPythonInterpreterFactoryConstants.Id64, typeof(CPythonInterpreterFactoryConstants))]
    [ProvidePythonInterpreterFactoryProvider("ConfigurablePythonInterpreterFactoryProvider", typeof(ConfigurablePythonInterpreterFactoryProvider))]
    [ProvidePythonInterpreterFactoryProvider(GuidList.guidLoadedProjectInterpreterFactoryProviderString, typeof(LoadedProjectInterpreterFactoryProvider))]
    [ProvideDiffSupportedContentType(".py;.pyw", ";")]
#if DEV11_OR_LATER // TODO: UNSURE IF WE NEED THIS FOR DEV12
    [ProvideX64DebuggerFixForIntegratedShell]
#endif
    [ProvideCodeExpansions(GuidList.guidPythonLanguageService, false, 106, "Python", @"Snippets\%LCID%\SnippetsIndex.xml", @"Snippets\%LCID%\Python\")]
    [ProvideCodeExpansionPath("Python", "Test", @"Snippets\%LCID%\Test\")]
#if DEV14_OR_LATER
    [ProvideInteractiveWindow(GuidList.guidPythonInteractiveWindow, Style = VsDockStyle.Linked, Orientation = ToolWindowOrientation.none, Window = ToolWindowGuids80.Outputwindow)]
#endif
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "Object is owned by VS and cannot be disposed")]
    public sealed class PythonToolsPackage : CommonPackage, IVsComponentSelectorProvider, IPythonToolsToolWindowService {
        [Obsolete("Services should be queried from an IServiceProvider flowed into the requesting component")]
        public static PythonToolsPackage Instance;
        private PythonAutomation _autoObject;
        private IContentType _contentType;
        private PackageContainer _packageContainer;
        internal const string PythonExpressionEvaluatorGuid = "{D67D5DB8-3D44-4105-B4B8-47AB1BA66180}";
        internal PythonToolsService _pyService;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public PythonToolsPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
#pragma warning disable 0618
            Instance = this;
#pragma warning restore 0618

#if DEBUG
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) => {
                if (!e.Observed) {
                    var str = e.Exception.ToString();
                    if (str.Contains("Python")) {
                        try {
                            ActivityLog.LogError(
                                "UnobservedTaskException",
                                string.Format("An exception in a task was not observed: {0}", e.Exception.ToString())
                            );
                        } catch (InvalidOperationException) {
                        }
                        Debug.Fail("An exception in a task was not observed. See ActivityLog.xml for more details.", e.Exception.ToString());
                    }
                    e.SetObserved();
                }
            };
#endif

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

        internal static void NavigateTo(System.IServiceProvider serviceProvider, string filename, Guid docViewGuidType, int line, int col) {
            VsUtilities.NavigateTo(serviceProvider, filename, docViewGuidType, line, col);
        }

        internal static void NavigateTo(System.IServiceProvider serviceProvider, string filename, Guid docViewGuidType, int pos) {
            IVsTextView viewAdapter;
            IVsWindowFrame pWindowFrame;
            VsUtilities.OpenDocument(serviceProvider, filename, out viewAdapter, out pWindowFrame);

            ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

            // Set the cursor at the beginning of the declaration.          
            int line, col;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetLineAndColumn(pos, out line, out col));
            ErrorHandler.ThrowOnFailure(viewAdapter.SetCaretPos(line, col));
            // Make sure that the text is visible.
            viewAdapter.CenterLines(line, 1);
        }

        internal static ITextBuffer GetBufferForDocument(System.IServiceProvider serviceProvider, string filename) {
            IVsTextView viewAdapter;
            IVsWindowFrame frame;
            VsUtilities.OpenDocument(serviceProvider, filename, out viewAdapter, out frame);

            IVsTextLines lines;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetBuffer(out lines));

            var adapter = serviceProvider.GetComponentModel().GetService<IVsEditorAdaptersFactoryService>();

            return adapter.GetDocumentBuffer(lines);
        }

        internal static IProjectLauncher GetLauncher(IServiceProvider serviceProvider, IPythonProject project) {
            var launchProvider = serviceProvider.GetUIThread().Invoke<string>(() => project.GetProperty(PythonConstants.LaunchProvider));

            IPythonLauncherProvider defaultLaunchProvider = null;
            foreach (var launcher in serviceProvider.GetComponentModel().GetExtensions<IPythonLauncherProvider>()) {
                if (launcher.Name == launchProvider) {
                    return serviceProvider.GetUIThread().Invoke<IProjectLauncher>(() => launcher.CreateLauncher(project));
                }

                if (launcher.Name == DefaultLauncherProvider.DefaultLauncherName) {
                    defaultLaunchProvider = launcher;
                }
            }

            // no launcher configured, use the default one.
            Debug.Assert(defaultLaunchProvider != null);
            return (defaultLaunchProvider != null) ?
                serviceProvider.GetUIThread().Invoke<IProjectLauncher>(() => defaultLaunchProvider.CreateLauncher(project)) :
                null;
        }

        void IPythonToolsToolWindowService.ShowWindowPane(Type windowType, bool focus) {
            var window = FindWindowPane(windowType, 0, true) as ToolWindowPane;
            if (window != null) {
                var frame = window.Frame as IVsWindowFrame;
                if (frame != null) {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                }
                if (focus) {
                    var content = window.Content as System.Windows.UIElement;
                    if (content != null) {
                        content.Focus();
                    }
                }
            }
        }

#if DEV14_OR_LATER
        protected override int CreateToolWindow(ref Guid toolWindowType, int id) {
            if (toolWindowType == GuidList.guidPythonInteractiveWindowGuid) {
                var res = ComponentModel.GetService<InteractiveWindowProvider>().CreateFromRegistry(
                    ComponentModel,
                    id
                );

                return res ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

            return base.CreateToolWindow(ref toolWindowType, id);
        }
#endif
        internal static void OpenNoInterpretersHelpPage(System.IServiceProvider serviceProvider, string page = null) {
            OpenVsWebBrowser(serviceProvider, page ?? PythonToolsInstallPath.GetFile("NoInterpreters.mht"));
        }

        public static string InterpreterHelpUrl {
            get {
                return string.Format("http://go.microsoft.com/fwlink/?LinkId=299429&clcid=0x{0:X}",
                    CultureInfo.CurrentCulture.LCID);
            }
        }

        protected override object GetAutomationObject(string name) {
            if (name == "VsPython") {
                return _autoObject;
            }

            return base.GetAutomationObject(name);
        }

        public override bool IsRecognizedFile(string filename) {
            return ModulePath.IsPythonSourceFile(filename);
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

        public PythonGeneralOptionsPage GeneralOptionsPage {
            get {
                return (PythonGeneralOptionsPage)GetDialogPage(typeof(PythonGeneralOptionsPage));
            }
        }

        public PythonDebuggingOptionsPage DebuggingOptionsPage {
            get {
                return (PythonDebuggingOptionsPage)GetDialogPage(typeof(PythonDebuggingOptionsPage));
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

        internal static void ShowOptionPage(System.IServiceProvider serviceProvider, Type dialogPage, IPythonInterpreterFactory interpreter) {
            if (dialogPage == typeof(PythonInterpreterOptionsPage)) {
                PythonInterpreterOptionsPage.NextOptionsSelection = interpreter;
            } else if (dialogPage == typeof(PythonInteractiveOptionsPage)) {
                PythonInteractiveOptionsPage.NextOptionsSelection = interpreter;
            } else {
                throw new InvalidOperationException();
            }

            serviceProvider.ShowOptionsPage(dialogPage);
        }


        /// <summary>
        /// Gets a CodeFormattingOptions object configured to match the current settings.
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use PythonToolsService.GetCodeFormattingOptions")]
        public CodeFormattingOptions GetCodeFormattingOptions() {
            return _pyService.GetCodeFormattingOptions();
        }

        /// <summary>
        /// Sets an individual code formatting option for the user's profile.
        /// <param name="name">a name of one of the properties on the CodeFormattingOptions class.</param>
        /// </summary>
        [Obsolete("Use PythonToolsService.SetFormattingOption")]
        public void SetFormattingOption(string name, object value) {
            _pyService.SetFormattingOption(name, value);
        }

        /// <summary>
        /// Gets an individual code formatting option as configured by the user.
        /// <param name="name">a name of one of the properties on the CodeFormattingOptions class.</param>
        /// <returns></returns>
        [Obsolete("Use PythonToolsService.GetFormattingOption")]
        public object GetFormattingOption(string name) {
            return _pyService.GetFormattingOption(name);
        }

        private new IComponentModel ComponentModel {
            get {
                return (IComponentModel)GetService(typeof(SComponentModel));
            }
        }

        /// <summary>
        /// The analyzer which is used for loose files.
        /// </summary>
        internal VsProjectAnalyzer DefaultAnalyzer {
            get {
                return _pyService.DefaultAnalyzer;
            }
        }

        internal PythonToolsService PythonService {
            get {
                return _pyService;
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

        internal static SettingsManager GetSettings(System.IServiceProvider serviceProvider) {
            return SettingsManagerCreator.GetSettingsManager(serviceProvider);
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

            var services = (IServiceContainer)this;

            // register our options service which provides registry access for various options
            var optionsService = new PythonToolsOptionsService(this);
            services.AddService(typeof(IPythonToolsOptionsService), optionsService, promote: true);

            services.AddService(typeof(IClipboardService), new ClipboardService(), promote: true);

            services.AddService(typeof(IPythonToolsToolWindowService), this, promote: true);

            // register our PythonToolsService which provides access to core PTVS functionality
            var pyService = _pyService = new PythonToolsService(services);

            services.AddService(typeof(PythonToolsService), pyService, promote: true);

            _autoObject = new PythonAutomation(this);

            services.AddService(
                typeof(ErrorTaskProvider),
                (container, serviceType) => {
                    var errorList = GetService(typeof(SVsErrorList)) as IVsTaskList;
                    var model = ComponentModel;
                    var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                    return new ErrorTaskProvider(this, errorList, errorProvider);
                },
                promote: true);

            services.AddService(
                typeof(CommentTaskProvider),
                (container, serviceType) => {
                    var taskList = GetService(typeof(SVsTaskList)) as IVsTaskList;
                    var model = ComponentModel;
                    var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                    return new CommentTaskProvider(this, taskList, errorProvider);
                },
                promote: true);

            var solutionEventListener = new SolutionEventsListener(this);
            solutionEventListener.StartListeningForChanges();

            services.AddService(
                typeof(SolutionEventsListener),
                solutionEventListener,
                promote: true
            );

#if DEV11_OR_LATER
            // Register custom debug event service
            var customDebuggerEventHandler = new CustomDebuggerEventHandler(this);
            services.AddService(customDebuggerEventHandler.GetType(), customDebuggerEventHandler, promote: true);

            // Enable the mixed-mode debugger UI context
            UIContext.FromUIContextGuid(DkmEngineId.NativeEng).IsActive = true;
#endif

            // Add our command handlers for menu (commands must exist in the .vsct file)
            RegisterCommands(new Command[] { 
                new OpenDebugReplCommand(this), 
                new ExecuteInReplCommand(this), 
                new SendToReplCommand(this), 
                new StartWithoutDebuggingCommand(this), 
                new StartDebuggingCommand(this), 
                new FillParagraphCommand(this), 
                new SendToDefiningModuleCommand(this), 
                new DiagnosticsCommand(this),
                new RemoveImportsCommand(this),
                new RemoveImportsCurrentScopeCommand(this),
                new OpenInterpreterListCommand(this),
                new ImportWizardCommand(this),
                new SurveyNewsCommand(this),
#if DEV11_OR_LATER
                new ShowPythonViewCommand(this),
                new ShowCppViewCommand(this),
                new ShowNativePythonFrames(this),
                new UsePythonStepping(this),
#endif
            }, GuidList.guidPythonToolsCmdSet);

#if FEATURE_AZURE_REMOTE_DEBUG
            try {
                RegisterCommands(new Command[] {
                    new AzureExplorerAttachDebuggerCommand(this)
                }, GuidList.guidPythonToolsCmdSet);
            } catch (NotSupportedException) {
            }
#endif


            RegisterCommands(GetReplCommands(), GuidList.guidPythonToolsCmdSet);

            RegisterProjectFactory(new PythonWebProjectFactory(this));

#if DEV11_OR_LATER
            // Enable the Python debugger UI context
            UIContext.FromUIContextGuid(AD7Engine.DebugEngineGuid).IsActive = true;
#endif

            var interpreterService = ComponentModel.GetService<IInterpreterOptionsService>();
            interpreterService.InterpretersChanged += RefreshReplCommands;
            interpreterService.DefaultInterpreterChanged += RefreshReplCommands;

            var loadedProjectProvider = interpreterService.KnownProviders
                .OfType<LoadedProjectInterpreterFactoryProvider>()
                .FirstOrDefault();
            // Ensure the provider is available - if not, you probably need to
            // rebuild or clean your experimental hive.
            Debug.Assert(loadedProjectProvider != null, "Expected LoadedProjectInterpreterFactoryProvider");
            if (loadedProjectProvider != null) {
                loadedProjectProvider.SetSolution((IVsSolution)GetService(typeof(SVsSolution)));
            }

            // The variable is inherited by child processes backing Test Explorer, and is used in PTVS
            // test discoverer and test executor to connect back to VS.
            Environment.SetEnvironmentVariable("_PTVS_PID", Process.GetCurrentProcess().Id.ToString());
        }

        private void RefreshReplCommands(object sender, EventArgs e) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) {
                return;
            }
            List<OpenReplCommand> replCommands = new List<OpenReplCommand>();
            lock (CommandsLock) {
                foreach (var keyValue in Commands) {
                    var command = keyValue.Key;
                    OpenReplCommand openRepl = command as OpenReplCommand;
                    if (openRepl != null) {
                        replCommands.Add(openRepl);

                        mcs.RemoveCommand(keyValue.Value);
                    }
                }

                foreach (var command in replCommands) {
                    Commands.Remove(command);
                }

                RegisterCommands(GetReplCommands(), GuidList.guidPythonToolsCmdSet);
            }
        }

        private List<OpenReplCommand> GetReplCommands() {
            var replCommands = new List<OpenReplCommand>();
            var interpreterService = ComponentModel.GetService<IInterpreterOptionsService>();
            var factories = interpreterService.Interpreters.ToList();
            if (factories.Count == 0) {
                return replCommands;
            }

            var defaultFactory = interpreterService.DefaultInterpreter;
            if (defaultFactory != interpreterService.NoInterpretersValue) {
                factories.Remove(defaultFactory);
                factories.Insert(0, defaultFactory);
            }

            for (int i = 0; i < (PkgCmdIDList.cmdidReplWindowF - PkgCmdIDList.cmdidReplWindow) && i < factories.Count; i++) {
                var factory = factories[i];

                var cmd = new OpenReplCommand(this, (int)PkgCmdIDList.cmdidReplWindow + i, factory);
                replCommands.Add(cmd);
            }

            if (defaultFactory != interpreterService.NoInterpretersValue) {
                // This command is a fallback for the Python.Interactive command
                // If no project is selected, the default environment will be
                // used.
                replCommands.Add(new OpenReplCommand(this, (int)PythonConstants.OpenInteractiveForEnvironment, defaultFactory));
            }
            return replCommands;
        }

        internal static bool TryGetStartupFileAndDirectory(System.IServiceProvider serviceProvider, out string filename, out string dir, out VsProjectAnalyzer analyzer) {
            var startupProject = GetStartupProject(serviceProvider);
            if (startupProject != null) {
                filename = startupProject.GetStartupFile();
                dir = startupProject.GetWorkingDirectory();
                analyzer = ((PythonProjectNode)startupProject).GetAnalyzer();
            } else {
                var textView = CommonPackage.GetActiveTextView(serviceProvider);
                if (textView == null) {
                    filename = null;
                    dir = null;
                    analyzer = null;
                    return false;
                }
                filename = textView.GetFilePath();
                analyzer = textView.GetAnalyzer(serviceProvider);
                dir = Path.GetDirectoryName(filename);
            }
            return true;
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions.AutoListMembers")]
        public bool AutoListMembers {
            get {
                return _pyService.LangPrefs.AutoListMembers;
            }
        }

        public EnvDTE.DTE DTE {
            get {
                return (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            }
        }

        [Obsolete("Use IServiceProvider.GetPythonContentType extension method")]
        public IContentType ContentType {
            get {
                if (_contentType == null) {
                    _contentType = ComponentModel.GetService<IContentTypeRegistryService>().GetContentType(PythonCoreConstants.ContentType);
                }
                return _contentType;
            }
        }

        [Obsolete("Use IServiceProvider.BrowseForFileOpen extension method")]
        public string BrowseForFileOpen(IntPtr owner, string filter, string initialPath = null) {
            return ((System.IServiceProvider)this).BrowseForFileOpen(owner, filter, initialPath);
        }

        [Obsolete("Use IServiceProvider.BrowseForFileSave extension method")]
        public string BrowseForFileSave(IntPtr owner, string filter, string initialPath = null) {
            return ((System.IServiceProvider)this).BrowseForFileSave(owner, filter, initialPath);
        }

        [Obsolete("Use IServiceProvider.BrowseForDirectory extension method")]
        public string BrowseForDirectory(IntPtr owner, string initialDirectory = null) {
            return ((System.IServiceProvider)this).BrowseForDirectory(owner, initialDirectory);
        }

        /// <summary>
        /// Creates a new Python REPL window which is independent from the
        /// default Python REPL windows.
        /// 
        /// This window will not persist across VS sessions.
        /// </summary>
        /// <param name="id">An ID which can be used to retrieve the window again and can survive across VS sessions.
        /// 
        /// The ID cannot include the | character.</param>
        /// <param name="title">The title of the window to be displayed</param>
        /// <param name="interpreter">The interpreter to be used.  This implies the language version and provides the path to the Python interpreter to be used.</param>
        /// <param name="startupFile">The file to be executed on the startup of the REPL.  Can be null, which will result in an interactive REPL.</param>
        /// <param name="workingDir">The working directory of the REPL process</param>
        /// <param name="project">The IVsHierarchy representing the Python project.</param>
        public IReplWindow CreatePythonRepl(string id, string title, IPythonInterpreterFactory interpreter, string workingDir, Dictionary<string, string> envVars = null, IVsHierarchy project = null) {
            Utilities.ArgumentNotNull("interpreter", interpreter);
            Utilities.ArgumentNotNull("id", id);
            Utilities.ArgumentNotNull("title", title);

            // The previous format of repl ID would produce new windows for
            // distinct working directories and/or env vars. To emulate this,
            // we now put all of these values into the user ID part, even though
            // they must still be manually provided after the evaluator is
            // created.
            var realId = string.Format(
                "{0};{1};{2}",
                id,
                workingDir ?? "",
                envVars == null ?
                    "" :
                    string.Join(";", envVars.Select(kvp => kvp.Key + "=" + kvp.Value))
            );

            string replId = PythonReplEvaluatorProvider.GetConfigurableReplId(realId);

#if DEV14_OR_LATER
            var replProvider = ComponentModel.GetService<InteractiveWindowProvider>();
            var vsWindow =
#else
            var replProvider = ComponentModel.GetService<IReplWindowProvider>();
            var window = 
#endif
            replProvider.FindReplWindow(replId) ?? replProvider.CreateReplWindow(
                this.GetPythonContentType(),
                title,
                typeof(PythonLanguageInfo).GUID,
                replId
            );
#if DEV14_OR_LATER
            var window = vsWindow.InteractiveWindow;
#endif
            var commandProvider = project as IPythonProject2;
            if (commandProvider != null) {
                commandProvider.AddActionOnClose((object)window, BasePythonReplEvaluator.CloseReplWindow);
            }

            var evaluator = window.Evaluator as BasePythonReplEvaluator;
            var options = (evaluator != null) ? evaluator.CurrentOptions as ConfigurablePythonReplOptions : null;
            if (options == null) {
                throw new NotSupportedException("Cannot modify options of " + window.Evaluator.GetType().FullName);
            }
            options.InterpreterFactory = interpreter;
            options.Project = project as PythonProjectNode;
            options._workingDir = workingDir;
            options._envVars = new Dictionary<string, string>(envVars);
            evaluator.Reset(quiet: true)
                .HandleAllExceptions(SR.ProductName, GetType())
                .DoNotWait();

            return window;
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

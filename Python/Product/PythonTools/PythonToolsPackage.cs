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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Repl;
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
using Microsoft.Win32;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;

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
    [ProvideMenuResource(1000, 1)]                              // This attribute is needed to let the shell know that this package exposes some menus.
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
    [ProvideDebugEngine(AD7Engine.DebugEngineName, typeof(AD7ProgramProvider), typeof(AD7Engine), AD7Engine.DebugEngineId)]
    [ProvideDebugLanguage("Python", "{DA3C7D59-F9E4-4697-BEE7-3A0703AF6BFF}", PythonExpressionEvaluatorGuid, AD7Engine.DebugEngineId)]
    [ProvideDebugPortSupplier("Python remote debugging (unsecured)", typeof(PythonRemoteDebugPortSupplierUnsecured), PythonRemoteDebugPortSupplierUnsecured.PortSupplierId)]
    [ProvideDebugPortSupplier("Python remote debugging (SSL)", typeof(PythonRemoteDebugPortSupplierSsl), PythonRemoteDebugPortSupplierSsl.PortSupplierId)]
    [ProvidePythonExecutionMode(ExecutionMode.StandardModeId, "Standard", "Standard")]
    [ProvidePythonExecutionMode("{91BB0245-B2A9-47BF-8D76-DD428C6D8974}", "IPython", "visualstudio_ipython_repl.IPythonBackend", supportsMultipleScopes: false, supportsMultipleCompleteStatementInputs: true)]
    [ProvidePythonExecutionMode("{3E390328-A806-4250-ACAD-97B5B37076E2}", "IPython w/o PyLab", "visualstudio_ipython_repl.IPythonBackendWithoutPyLab", supportsMultipleScopes: false, supportsMultipleCompleteStatementInputs: true)]
    #region Exception List
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ArithmeticError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.AssertionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.AttributeError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.BaseException")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.BufferError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.BytesWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.DeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.EOFError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.EnvironmentError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.Exception")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.FloatingPointError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.FutureWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.GeneratorExit", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.IOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ImportError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ImportWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.IndentationError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.IndexError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.KeyError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.KeyboardInterrupt")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.LookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.MemoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.NameError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.NotImplementedError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.BlockingIOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ChildProcessError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ConnectionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ConnectionError", "exceptions.BrokenPipeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ConnectionError", "exceptions.ConnectionAbortedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ConnectionError", "exceptions.ConnectionRefusedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ConnectionError", "exceptions.ConnectionResetError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.FileExistsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.FileNotFoundError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.InterruptedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.IsADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.NotADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.PermissionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.ProcessLookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OSError", "exceptions.TimeoutError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.OverflowError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.PendingDeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ReferenceError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.RuntimeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.RuntimeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.StandardError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.StopIteration", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.SyntaxError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.SyntaxWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.SystemError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.SystemExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.TabError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.TypeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnboundLocalError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnicodeDecodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnicodeEncodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnicodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnicodeTranslateError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UnicodeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.UserWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ValueError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.Warning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.WindowsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 2.x", "exceptions", "exceptions.ZeroDivisionError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ArithmeticError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.AssertionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.AttributeError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.BaseException")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.BufferError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.BytesWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.DeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.EOFError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.EnvironmentError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.Exception")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.FloatingPointError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.FutureWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.GeneratorExit", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.IOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ImportError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ImportWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.IndentationError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.IndexError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.KeyError", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.KeyboardInterrupt")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.LookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.MemoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.NameError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.NotImplementedError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.BlockingIOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ChildProcessError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ConnectionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ConnectionError", "builtins.BrokenPipeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ConnectionError", "builtins.ConnectionAbortedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ConnectionError", "builtins.ConnectionRefusedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ConnectionError", "builtins.ConnectionResetError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.FileExistsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.FileNotFoundError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.InterruptedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.IsADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.NotADirectoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.PermissionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.ProcessLookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OSError", "builtins.TimeoutError")]

    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.OverflowError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.PendingDeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ReferenceError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.RuntimeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.RuntimeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.StandardError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.StopIteration", State = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.SyntaxError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.SyntaxWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.SystemError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.SystemExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.TabError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.TypeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnboundLocalError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnicodeDecodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnicodeEncodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnicodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnicodeTranslateError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UnicodeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.UserWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ValueError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.Warning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.WindowsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "Python 3.x", "builtins", "builtins.ZeroDivisionError")]
    #endregion
    [ProvideComponentPickerPropertyPage(typeof(PythonToolsPackage), typeof(WebPiComponentPickerControl), "WebPi", DefaultPageNameValue = "#4000")]
    [ProvideToolWindow(typeof(InterpreterListToolWindow), Style = VsDockStyle.Linked, Window = ToolWindowGuids80.Outputwindow)]
    [ProvidePythonInterpreterFactoryProvider(CPythonInterpreterFactoryConstants.Id32, typeof(CPythonInterpreterFactoryConstants))]
    [ProvidePythonInterpreterFactoryProvider(CPythonInterpreterFactoryConstants.Id64, typeof(CPythonInterpreterFactoryConstants))]
    [ProvidePythonInterpreterFactoryProvider("ConfigurablePythonInterpreterFactoryProvider", typeof(ConfigurablePythonInterpreterFactoryProvider))]
    [ProvideDiffSupportedContentType(".py;.pyw", ";")]
#if DEV11_OR_LATER // TODO: UNSURE IF WE NEED THIS FOR DEV12
    [ProvideX64DebuggerFixForIntegratedShell]
#endif
    public sealed class PythonToolsPackage : CommonPackage, IVsComponentSelectorProvider {
        private LanguagePreferences _langPrefs;
        public static PythonToolsPackage Instance;
        private VsProjectAnalyzer _analyzer;
        private PythonAutomation _autoObject = new PythonAutomation();
        private IContentType _contentType;
        private PackageContainer _packageContainer;
        internal const string PythonExpressionEvaluatorGuid = "{D67D5DB8-3D44-4105-B4B8-47AB1BA66180}";
        private SolutionEventsListener _solutionEventListener;
        private string _surveyNewsUrl;
        private object _surveyNewsUrlLock = new object();
        private PythonToolsLogger _logger;

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

        internal PythonToolsLogger Logger {
            get {
                return _logger;
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

            var adapter = ComponentModel.GetService<IVsEditorAdaptersFactoryService>();

            return adapter.GetDocumentBuffer(lines);
        }

        internal static IProjectLauncher GetLauncher(IPythonProject project) {
            var launchProvider = UIThread.Instance.RunSync<string>(() => project.GetProperty(PythonConstants.LaunchProvider));

            IPythonLauncherProvider defaultLaunchProvider = null;
            foreach (var launcher in ComponentModel.GetExtensions<IPythonLauncherProvider>()) {
                if (launcher.Name == launchProvider) {
                    return UIThread.Instance.RunSync<IProjectLauncher>(() => launcher.CreateLauncher(project));
                }

                if (launcher.Name == DefaultLauncherProvider.DefaultLauncherName) {
                    defaultLaunchProvider = launcher;
                }
            }

            // no launcher configured, use the default one.
            Debug.Assert(defaultLaunchProvider != null);
            return (defaultLaunchProvider != null) ?
                UIThread.Instance.RunSync<IProjectLauncher>(() => defaultLaunchProvider.CreateLauncher(project)) :
                null;
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

        internal void ShowInterpreterList() {
            var window = FindWindowPane(typeof(InterpreterListToolWindow), 0, true) as ToolWindowPane;
            if (window != null) {
                var frame = window.Frame as IVsWindowFrame;
                if (frame != null) {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                }
                var content = window.Content as System.Windows.UIElement;
                if (content != null) {
                    content.Focus();
                }
            }
        }

        internal static void OpenNoInterpretersHelpPage() {
            OpenVsWebBrowser(PythonToolsInstallPath.GetFile("NoInterpreters.html"));
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

        internal IPythonInterpreterFactory NextOptionsSelection { get; set; }

        internal void ShowOptionPage(Type dialogPage, IPythonInterpreterFactory interpreter) {
            NextOptionsSelection = interpreter;
            ShowOptionPage(dialogPage);
        }


        /// <summary>
        /// Gets a CodeFormattingOptions object configured to match the current settings.
        /// </summary>
        /// <returns></returns>
        public CodeFormattingOptions GetCodeFormattingOptions() {
            return ((PythonFormattingSpacingOptionsPage)GetDialogPage(typeof(PythonFormattingSpacingOptionsPage))).GetCodeFormattingOptions();
        }

        /// <summary>
        /// Sets an individual code formatting option for the user's profile.
        /// <param name="name">a name of one of the properties on the CodeFormattingOptions class.</param>
        /// </summary>
        public void SetFormattingOption(string name, object value) {
            PythonFormattingOptionsPage.SetOption(name, value);
        }

        /// <summary>
        /// Gets an individual code formatting option as configured by the user.
        /// <param name="name">a name of one of the properties on the CodeFormattingOptions class.</param>
        /// <returns></returns>
        public object GetFormattingOption(string name) {
            return PythonFormattingOptionsPage.GetOption(name);
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
            var interpreterService = ComponentModel.GetService<IInterpreterOptionsService>();
            var defaultFactory = interpreterService.DefaultInterpreter;
            EnsureCompletionDb(defaultFactory);
            return new VsProjectAnalyzer(
                defaultFactory.CreateInterpreter(),
                defaultFactory,
                interpreterService.Interpreters.ToArray(),
                ComponentModel.GetService<IErrorProviderFactory>());
        }

        /// <summary>
        /// Asks the interpreter to generate its completion database if the
        /// option is enabled (the default) and the database is not current.
        /// </summary>
        internal static void EnsureCompletionDb(IPythonInterpreterFactory factory) {
            if (PythonToolsPackage.Instance.DebuggingOptionsPage.AutoAnalyzeStandardLibrary) {
                var withDb = factory as IPythonInterpreterFactoryWithDatabase;
                if (withDb != null && !withDb.IsCurrent) {
                    withDb.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
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

        internal static SettingsManager Settings {
            get {
                return SettingsManagerCreator.GetSettingsManager(Instance);
            }
        }

        /// <summary>
        /// Returns the Visual Studio hive for the current user's settings.
        /// 
        /// Settings that are covered by Import/Export settings should be stored
        /// here.
        /// 
        /// This is typically something like:
        ///     HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\10.0
        /// 
        /// The caller is responsible for closing the returned key.
        /// </summary>
        internal static new RegistryKey UserRegistryRoot {
            get {
                try {
                    if (Instance != null) {
                        return ((CommonPackage)Instance).UserRegistryRoot;
                    }
                    return VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, writable: true);
                } catch (ArgumentException) {
                    // Thrown if we can't use VSRegistry, which typically means
                    // that there's no global service provider.
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32)) {
                        return root.OpenSubKey("Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion, writable: true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the Visual Studio hive in HKEY_LOCAL_MACHINE.
        /// 
        /// This is typically something like:
        ///     HKEY_LOCAL_MACHINE\Software\Microsoft\VisualStudio\10.0
        /// 
        /// The returned key is not writable.
        /// 
        /// The caller is responsible for closing the returned key.
        /// </summary>
        internal static new RegistryKey ApplicationRegistryRoot {
            get {
                if (Instance != null) {
                    return ((CommonPackage)Instance).ApplicationRegistryRoot;
                }

                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) {
                    return root.OpenSubKey("Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion, writable: false);
                }
            }
        }

        /// <summary>
        /// Returns the Visual Studio hive for the current user's configuration.
        /// 
        /// Information obtained from pkgdef files is written to this hive.
        /// 
        /// This is typically something like:
        ///     HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\10.0_Config
        /// 
        /// The caller is responsible for closing the returned key.
        /// </summary>
        internal static RegistryKey ConfigurationRegistryRoot {
            get {
                try {
                    if (Instance != null) {
                        return VSRegistry.RegistryRoot(Instance, __VsLocalRegistryType.RegType_Configuration, writable: true);
                    }
                    return VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration, writable: true);
                } catch (ArgumentException) {
                    // Thrown if we can't use VSRegistry, which typically means
                    // that there's no global service provider.
                    return Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion + "_Config", writable: true);
                }
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

            _solutionEventListener = new SolutionEventsListener(this);
            _solutionEventListener.StartListeningForChanges();

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

#if DEV11_OR_LATER
            // Register custom debug event service
            var customDebuggerEventHandler = new CustomDebuggerEventHandler();
            ((IServiceContainer)this).AddService(customDebuggerEventHandler.GetType(), customDebuggerEventHandler, promote: true);
#endif

            // Add our command handlers for menu (commands must exist in the .vsct file)
            RegisterCommands(new Command[] { 
                new OpenDebugReplCommand(), 
                new ExecuteInReplCommand(), 
                new SendToReplCommand(), 
                new StartWithoutDebuggingCommand(), 
                new StartDebuggingCommand(), 
                new FillParagraphCommand(), 
                new SendToDefiningModuleCommand(), 
                new DiagnosticsCommand(),
                new RemoveImportsCommand(),
                new RemoveImportsCurrentScopeCommand(),
                new OpenInterpreterListCommand(),
                new ImportWizardCommand(),
                new SurveyNewsCommand(),
#if DEV11_OR_LATER
                new ShowPythonViewCommand(),
                new ShowCppViewCommand(),
                new ShowNativePythonFrames(),
                new UsePythonStepping(),
#endif
            }, GuidList.guidPythonToolsCmdSet);

            RegisterCommands(GetReplCommands(), GuidList.guidPythonToolsCmdSet);

            RegisterProjectFactory(new PythonWebProjectFactory(this));

            var interpreterService = ComponentModel.GetService<IInterpreterOptionsService>();
            interpreterService.InterpretersChanged += RefreshReplCommands;
            interpreterService.DefaultInterpreterChanged += RefreshReplCommands;
            interpreterService.DefaultInterpreterChanged += UpdateDefaultAnalyzer;

            InitializeLogging(interpreterService);
        }

        private void InitializeLogging(IInterpreterOptionsService interpService) {
            _logger = new PythonToolsLogger(ComponentModel.GetExtensions<IPythonToolsLogger>().ToArray());

            // log interesting stats on startup
            var installed = interpService.KnownProviders
                .Where(x => !(x is ConfigurablePythonInterpreterFactoryProvider))
                .SelectMany(x => x.GetInterpreterFactories())
                .Count();

            var configured = interpService.KnownProviders.
                Where(x => x is ConfigurablePythonInterpreterFactoryProvider).
                SelectMany(x => x.GetInterpreterFactories())
                .Count();

            _logger.LogEvent(PythonLogEvent.InstalledInterpreters, installed);
            _logger.LogEvent(PythonLogEvent.ConfiguredInterpreters, configured);
        }

        internal SolutionEventsListener SolutionEvents {
            get {
                return _solutionEventListener;
            }
        }

        internal void GlobalInvoke(CommandID cmdID) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            mcs.GlobalInvoke(cmdID);
        }

        internal void GlobalInvoke(CommandID cmdID, object arg) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            mcs.GlobalInvoke(cmdID, arg);
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

        public string BrowseForFileOpen(IntPtr owner, string filter, string initialPath = null) {
            if (string.IsNullOrEmpty(initialPath)) {
                initialPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + Path.DirectorySeparatorChar;
            }

            IVsUIShell uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var sfd = new System.Windows.Forms.OpenFileDialog()) {
                    sfd.AutoUpgradeEnabled = true;
                    sfd.Filter = filter;
                    sfd.FileName = Path.GetFileName(initialPath);
                    sfd.InitialDirectory = Path.GetDirectoryName(initialPath);
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = sfd.ShowDialog();
                    } else {
                        result = sfd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return sfd.FileName;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSOPENFILENAMEW[] openInfo = new VSOPENFILENAMEW[1];
            openInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSOPENFILENAMEW));
            openInfo[0].pwzFilter = filter.Replace('|', '\0') + "\0";
            openInfo[0].hwndOwner = owner;
            openInfo[0].nMaxFileName = 260;
            var pFileName = Marshal.AllocCoTaskMem(520);
            openInfo[0].pwzFileName = pFileName;
            openInfo[0].pwzInitialDir = Path.GetDirectoryName(initialPath);
            var nameArray = (Path.GetFileName(initialPath) + "\0").ToCharArray();
            Marshal.Copy(nameArray, 0, pFileName, nameArray.Length);
            try {
                int hr = uiShell.GetOpenFileNameViaDlg(openInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(openInfo[0].pwzFileName);
            } finally {
                if (pFileName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pFileName);
                }
            }
        }

        public string BrowseForFileSave(IntPtr owner, string filter, string initialPath = null) {
            if (string.IsNullOrEmpty(initialPath)) {
                initialPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + Path.DirectorySeparatorChar;
            }

            IVsUIShell uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var sfd = new System.Windows.Forms.SaveFileDialog()) {
                    sfd.AutoUpgradeEnabled = true;
                    sfd.Filter = filter;
                    sfd.FileName = Path.GetFileName(initialPath);
                    sfd.InitialDirectory = Path.GetDirectoryName(initialPath);
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = sfd.ShowDialog();
                    } else {
                        result = sfd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return sfd.FileName;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSSAVEFILENAMEW[] saveInfo = new VSSAVEFILENAMEW[1];
            saveInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSSAVEFILENAMEW));
            saveInfo[0].pwzFilter = filter.Replace('|', '\0') + "\0";
            saveInfo[0].hwndOwner = owner;
            saveInfo[0].nMaxFileName = 260;
            var pFileName = Marshal.AllocCoTaskMem(520);
            saveInfo[0].pwzFileName = pFileName;
            saveInfo[0].pwzInitialDir = Path.GetDirectoryName(initialPath);
            var nameArray = (Path.GetFileName(initialPath) + "\0").ToCharArray();
            Marshal.Copy(nameArray, 0, pFileName, nameArray.Length);
            try {
                int hr = uiShell.GetSaveFileNameViaDlg(saveInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(saveInfo[0].pwzFileName);
            } finally {
                if (pFileName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pFileName);
                }
            }
        }

        public string BrowseForDirectory(IntPtr owner, string initialDirectory = null) {
            IVsUIShell uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var ofd = new FolderBrowserDialog()) {
                    ofd.RootFolder = Environment.SpecialFolder.Desktop;
                    ofd.ShowNewFolderButton = false;
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = ofd.ShowDialog();
                    } else {
                        result = ofd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return ofd.SelectedPath;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSBROWSEINFOW[] browseInfo = new VSBROWSEINFOW[1];
            browseInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
            browseInfo[0].pwzInitialDir = initialDirectory;
            browseInfo[0].hwndOwner = owner;
            browseInfo[0].nMaxDirName = 260;
            IntPtr pDirName = Marshal.AllocCoTaskMem(520);
            browseInfo[0].pwzDirName = pDirName;
            try {
                int hr = uiShell.GetDirectoryViaBrowseDlg(browseInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(browseInfo[0].pwzDirName);
            } finally {
                if (pDirName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pDirName);
                }
            }
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

            var replProvider = ComponentModel.GetService<IReplWindowProvider>();

            var window = replProvider.FindReplWindow(replId) ?? replProvider.CreateReplWindow(
                ContentType,
                title,
                typeof(PythonLanguageInfo).GUID,
                replId
            );

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
            evaluator.Reset(quiet: true);

            return window;
        }

        private void BrowseSurveyNewsOnIdle(object sender, ComponentManagerEventArgs e) {
            this.OnIdle -= BrowseSurveyNewsOnIdle;

            lock (_surveyNewsUrlLock) {
                if (!string.IsNullOrEmpty(_surveyNewsUrl)) {
                    OpenVsWebBrowser(_surveyNewsUrl);
                    _surveyNewsUrl = null;
                }
            }
        }

        internal void BrowseSurveyNews(string url) {
            lock (_surveyNewsUrlLock) {
                _surveyNewsUrl = url;
            }

            this.OnIdle += BrowseSurveyNewsOnIdle;
        }

        private void CheckSurveyNewsThread(Uri url, bool warnIfNoneAvailable) {
            // We can't use a simple WebRequest, because that doesn't have access
            // to the browser's session cookies.  Cookies are used to remember
            // which survey/news item the user has submitted/accepted.  The server 
            // checks the cookies and returns the survey/news urls that are 
            // currently available (availability is determined via the survey/news 
            // item start and end date).
            var th = new Thread(() => {
                var br = new WebBrowser();
                br.Tag = warnIfNoneAvailable;
                br.DocumentCompleted += OnSurveyNewsDocumentCompleted;
                br.Navigate(url);
                Application.Run();
            });
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        private void OnSurveyNewsDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
            var br = (WebBrowser)sender;
            var warnIfNoneAvailable = (bool)br.Tag;
            if (br.Url == e.Url) {
                List<string> available = null;

                string json = br.DocumentText;
                if (!string.IsNullOrEmpty(json)) {
                    int startIndex = json.IndexOf("<PRE>");
                    if (startIndex > 0) {
                        int endIndex = json.IndexOf("</PRE>", startIndex);
                        if (endIndex > 0) {
                            json = json.Substring(startIndex + 5, endIndex - startIndex - 5);

                            try {
                                // Example JSON data returned by the server:
                                //{
                                // "cannotvoteagain": [], 
                                // "notvoted": [
                                //  "http://ptvs.azurewebsites.net/news/141", 
                                //  "http://ptvs.azurewebsites.net/news/41", 
                                // ], 
                                // "canvoteagain": [
                                //  "http://ptvs.azurewebsites.net/news/51"
                                // ]
                                //}

                                // Description of each list:
                                // voted: cookie found
                                // notvoted: cookie not found
                                // canvoteagain: cookie found, but multiple votes are allowed
                                JavaScriptSerializer serializer = new JavaScriptSerializer();
                                var results = serializer.Deserialize<Dictionary<string, List<string>>>(json);
                                available = results["notvoted"];
                            } catch (ArgumentException) {
                            } catch (InvalidOperationException) {
                            }
                        }
                    }
                }

                if (available != null && available.Count > 0) {
                    BrowseSurveyNews(available[0]);
                } else if (warnIfNoneAvailable) {
                        if (available != null) {
                        BrowseSurveyNews(GeneralOptionsPage.SurveyNewsIndexUrl);
                        } else {
                        BrowseSurveyNews(PythonToolsInstallPath.GetFile("NoSurveyNewsFeed.html"));
                    }
                }

                Application.ExitThread();
            }
        }

        internal void CheckSurveyNews(bool forceCheckAndWarnIfNoneAvailable) {
            bool shouldQueryServer = false;
            if (forceCheckAndWarnIfNoneAvailable) {
                shouldQueryServer = true;
            } else {
                var options = GeneralOptionsPage;
                // Ensure that we don't prompt the user on their very first project creation.
                // Delay by 3 days by pretending we checked 4 days ago (the default of check
                // once a week ensures we'll check again in 3 days).
                if (options.SurveyNewsLastCheck == DateTime.MinValue) {
                    options.SurveyNewsLastCheck = DateTime.Now - TimeSpan.FromDays(4);
                    options.SaveSettingsToStorage();
                }

                var elapsedTime = DateTime.Now - options.SurveyNewsLastCheck;
                switch (options.SurveyNewsCheck) {
                    case SurveyNewsPolicy.Disabled:
                        break;
                    case SurveyNewsPolicy.CheckOnceDay:
                        shouldQueryServer = elapsedTime.TotalDays >= 1;
                        break;
                    case SurveyNewsPolicy.CheckOnceWeek:
                        shouldQueryServer = elapsedTime.TotalDays >= 7;
                        break;
                    case SurveyNewsPolicy.CheckOnceMonth:
                        shouldQueryServer = elapsedTime.TotalDays >= 30;
                        break;
                    default:
                        Debug.Assert(false, String.Format("Unexpected SurveyNewsPolicy: {0}.", options.SurveyNewsCheck));
                        break;
                }
            }

            if (shouldQueryServer) {
                var options = GeneralOptionsPage;
                options.SurveyNewsLastCheck = DateTime.Now;
                options.SaveSettingsToStorage();
                CheckSurveyNewsThread(new Uri(options.SurveyNewsFeedUrl), forceCheckAndWarnIfNoneAvailable);
            }
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

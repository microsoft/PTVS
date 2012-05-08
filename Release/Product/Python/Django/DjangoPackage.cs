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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EnvDTE90a;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Django.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Django {
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
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideEditorExtension(typeof(DjangoEditorFactory), ".djt", 50,
              ProjectGuid = "{A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3}",
              TemplateDir = "Templates",
              NameResourceID = 102,
              DefaultName = "webpage")]
    [ProvideLanguageService(typeof(DjangoLanguageInfo), "Django Templates", 107, RequestStockColors = true, ShowSmartIndent = true, ShowCompletion = true, DefaultToInsertSpaces = true, HideAdvancedMembersByDefault = false, EnableAdvancedMembersOption = true, ShowDropDownOptions = true)]
    [ProvideLanguageExtension(typeof(DjangoLanguageInfo), ".djt")]
    [ProvideDebugLanguage("Django Templates", DjangoTemplateLanguageId, "{" + DjangoExpressionEvaluatorGuid + "}", "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}")]
    [ProvideEditorExtension2(typeof(DjangoEditorFactory), ".djt", 50, "*:1", ProjectGuid = "{A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3}", NameResourceID = 102, DefaultName = "webpage")]
    [ProvideEditorExtension2(typeof(DjangoEditorFactoryPromptForEncoding), ".djt", 50, "*:1", ProjectGuid = "{A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3}", NameResourceID = 113, DefaultName = "webpage")]
    [ProvideKeyBindingTable(GuidList.guidDjangoEditorFactoryString, 102)]
    [ProvideEditorLogicalView(typeof(DjangoEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorLogicalView(typeof(DjangoEditorFactoryPromptForEncoding), VSConstants.LOGVIEWID.TextView_string)]
    [Guid(GuidList.guidDjangoPkgString)]
    [ProvideObject(typeof(DjangoProject), RegisterUsing = RegistrationMethod.CodeBase)]
    [ProvideProjectFactory(typeof(DjangoProjectFactory), "Django/Python", "Django Project Files (*.pyproj);*.pyproj", "pyproj", "pyproj", ".\\NullPath")]
    public sealed class DjangoPackage : Package {
        internal const string DjangoTemplateLanguageId = "{918E5764-7026-4D57-918D-19D86AD73AC4}";
        internal const string DjangoExpressionEvaluatorGuid = "64F20547-C246-487F-83A6-587BC54BAB2F";
        internal static Guid DjangoTemplateLanguageGuid = new Guid(DjangoTemplateLanguageId);

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public DjangoPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            var langService = new DjangoLanguageInfo(this);
            ((IServiceContainer)this).AddService(langService.GetType(), langService, true);

            //Create Editor Factory. Note that the base Package class will call Dispose on it.
            RegisterEditorFactory(new DjangoEditorFactory(this));
            RegisterEditorFactory(new DjangoEditorFactoryPromptForEncoding(this));
            RegisterProjectFactory(new DjangoProjectFactory(this));
            
            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidDjangoCmdSet, (int)PkgCmdIDList.cmdidGotoTemplateSource);
                MenuCommand menuItem = new MenuCommand(GotoTemplateSourceCode, menuCommandID);
                mcs.AddCommand(menuItem);
            }
        }

        private void GotoTemplateSourceCode(object sender, EventArgs args) {
            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));

            var curFrame = (StackFrame2)dte.Debugger.CurrentStackFrame;
            
            var frameId = curFrame.Depth;
            var thread = curFrame.Parent;
            var threadId = thread.ID;
            var process = thread.Program;
            var processId = process.Process.ProcessID;

            var mappingDoc = AD7Engine.GetCodeMappingDocument(processId, threadId, (int)(frameId - 1));
            if(mappingDoc != null) {
                var debugger = (IVsDebugger2)GetService(typeof(IVsDebugger));
                IVsTextView view;
                ErrorHandler.ThrowOnFailure(debugger.ShowSource(mappingDoc, 1, 1, 1, 0, out view));
            }
        }

        #endregion

        internal new object GetService(Type serviceType) {
            return base.GetService(serviceType);
        }
    }
}

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
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
#if DEV11
using Microsoft.VisualStudio.Shell.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    //Set the projectsTemplatesDirectory to a non-existant path to prevent VS from including the working directory as a valid template path
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Description("Python Project Package")]
    [ProvideProjectFactory(typeof(PythonProjectFactory), PythonConstants.LanguageName, PythonFileFilter, "pyproj", "pyproj", ".\\NullPath", LanguageVsTemplate = PythonConstants.LanguageName)]
    [ProvideObject(typeof(PythonGeneralPropertyPage))]
    [ProvideObject(typeof(PythonDebugPropertyPage))]
    [ProvideObject(typeof(PublishPropertyPage))]
    [ProvideEditorExtension(typeof(PythonEditorFactory), PythonConstants.FileExtension, 50, ProjectGuid = VSConstants.CLSID.MiscellaneousFilesProject_string, NameResourceID = 3004, DefaultName = "module", TemplateDir = "Templates\\NewItem")]
    [ProvideFileExtensionMapping("{E23E32ED-3467-4401-A364-1352666A3502}", "Python Editor", typeof(PythonEditorFactory), "{" + PythonConstants.ProjectSystemPackageGuid + "}", 100)]
#if DEV11
    [ProvideEditorExtension2(typeof(PythonEditorFactory), PythonConstants.FileExtension, 50, __VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, "*:1", ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3004, DefaultName = "module", TemplateDir = ".\\NullPath")]
    [ProvideEditorExtension2(typeof(PythonEditorFactory), PythonConstants.WindowsFileExtension, 50, __VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, null, ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3004, DefaultName = "module", TemplateDir = ".\\NullPath")]
    [ProvideEditorExtension2(typeof(PythonEditorFactoryPromptForEncoding), PythonConstants.FileExtension, 50, __VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3015, LinkedEditorGuid = PythonConstants.EditorFactoryGuid, TemplateDir = ".\\NullPath")]
#else
    [ProvideEditorExtension2(typeof(PythonEditorFactory), PythonConstants.FileExtension, 50, "*:1", ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3004, DefaultName = "module", TemplateDir = ".\\NullPath")]
    [ProvideEditorExtension2(typeof(PythonEditorFactory), PythonConstants.WindowsFileExtension, 50, ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3004, DefaultName = "module", TemplateDir = ".\\NullPath")]
    [ProvideEditorExtension2(typeof(PythonEditorFactoryPromptForEncoding), PythonConstants.FileExtension, 50, ProjectGuid = PythonConstants.ProjectFactoryGuid, NameResourceID = 3015, LinkedEditorGuid = PythonConstants.EditorFactoryGuid, TemplateDir = ".\\NullPath")]
#endif
    [ProvideFileFilter(PythonConstants.ProjectFactoryGuid, "/1", "Python Files;*.py,*.pyw", 100)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactory), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactory), VSConstants.LOGVIEWID.Debugging_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactoryPromptForEncoding), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactoryPromptForEncoding), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactoryPromptForEncoding), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(PythonEditorFactoryPromptForEncoding), VSConstants.LOGVIEWID.Debugging_string)]
    [Guid(PythonConstants.ProjectSystemPackageGuid)]
    [DeveloperActivity("Python", typeof(PythonProjectPackage))]
    public class PythonProjectPackage : CommonProjectPackage {
        internal const string PythonFileFilter = "Python Project Files (*.pyproj);*.pyproj";

        public override ProjectFactory CreateProjectFactory() {
            return new PythonProjectFactory(this);
        }

        public override CommonEditorFactory CreateEditorFactory() {
            return new PythonEditorFactory(this);
        }

        public override CommonEditorFactory CreateEditorFactoryPromptForEncoding() {
            return new PythonEditorFactoryPromptForEncoding(this);
        }

        /// <summary>
        /// This method is called to get the icon that will be displayed in the
        /// Help About dialog when this package is selected.
        /// </summary>
        /// <returns>The resource id corresponding to the icon to display on the Help About dialog</returns>
        public override uint GetIconIdForAboutBox() {
            return PythonConstants.IconIdForAboutBox;
        }
        /// <summary>
        /// This method is called during Devenv /Setup to get the bitmap to
        /// display on the splash screen for this package.
        /// </summary>
        /// <returns>The resource id corresponding to the bitmap to display on the splash screen</returns>
        public override uint GetIconIdForSplashScreen() {
            return PythonConstants.IconIfForSplashScreen;
        }
        /// <summary>
        /// This methods provides the product official name, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public override string GetProductName() {
            return PythonConstants.LanguageName;
        }

        /// <summary>
        /// This methods provides the product description, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public override string GetProductDescription() {
            return PythonConstants.LanguageName;
            //return Resources.ProductDescription;
        }
        /// <summary>
        /// This methods provides the product version, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public override string GetProductVersion() {
            return this.GetType().Assembly.GetName().Version.ToString();
        }
    }
}

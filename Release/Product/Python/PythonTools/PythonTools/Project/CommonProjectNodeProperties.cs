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
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio.Shell.Interop;


namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class CommonProjectNodeProperties : ProjectNodeProperties, IVsCfgBrowseObject, VSLangProj.ProjectProperties {

        public CommonProjectNodeProperties(ProjectNode node)
            : base(node) {
        }

        #region properties
        /// <summary>
        /// Returns/Sets the StartupFile project property
        /// </summary>
        [Browsable(false)]
        public string StartupFile {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.StartupFile, true);
            }
            set {
                this.Node.ProjectMgr.SetProjectProperty(CommonConstants.StartupFile, value);
            }
        }

        /// <summary>
        /// Returns/Sets the StartupFile project property
        /// </summary>
        [Browsable(false)]
        public string WorkingDirectory {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.WorkingDirectory, true);
            }
            set {
                this.Node.ProjectMgr.SetProjectProperty(CommonConstants.WorkingDirectory, value);
            }
        }

        /// <summary>
        /// Returns/Sets the PublishUrl project property which is where the project is published to
        /// </summary>
        [Browsable(false)]
        public string PublishUrl {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.PublishUrl, true);
            }
            set {
                this.Node.ProjectMgr.SetProjectProperty(CommonConstants.PublishUrl, value);
            }
        }

        /// <summary>
        /// Returns the SearchPath project property
        /// </summary>
        [Browsable(false)]
        public string SearchPath {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.SearchPath, true);
            }
        }

        /// <summary>
        /// Gets the command line arguments for the project.
        /// </summary>
        [Browsable(false)]
        public string CommandLineArguments {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.CommandLineArguments, true);
            }
        }

        /// <summary>
        /// Gets the override for the interpreter path to used for launching the project.
        /// </summary>
        [Browsable(false)]
        public string InterpreterPath {
            get {
                var res = this.Node.ProjectMgr.GetProjectProperty(CommonConstants.InterpreterPath, true);
                if (!string.IsNullOrEmpty(res)) {
                    var proj = Node.ProjectMgr as CommonProjectNode;
                    if (proj != null) {
                        res = CommonUtils.GetAbsoluteFilePath(proj.GetWorkingDirectory(), res);
                    }
                }
                return res;
            }
        }

        //We don't need this property, but still have to provide it, otherwise
        //Add New Item wizard (which seems to be unmanaged) fails.
        [Browsable(false)]
        public string RootNamespace {
            get {
                return "";
            }
            set {
                //Do nothing
            }
        }

        /// <summary>
        /// Gets the home directory for the project.
        /// </summary>
        [Browsable(false)]
        public string ProjectHome {
            get {
                return Node.ProjectMgr.ProjectHome;
            }
        }

        #endregion

        #region IVsCfgBrowseObject Members

        int IVsCfgBrowseObject.GetCfg(out IVsCfg ppCfg) {
            return Node.ProjectMgr.ConfigProvider.GetCfgOfName(
                Node.ProjectMgr.CurrentConfig.GetPropertyValue(ProjectFileConstants.Configuration),
                Node.ProjectMgr.CurrentConfig.GetPropertyValue(ProjectFileConstants.Platform),
                out ppCfg);
        }

        #endregion

        #region ProjectProperties Members
        
        [Browsable(false)]
        public string AbsoluteProjectDirectory {
            get {
                return Node.ProjectMgr.ProjectFolder;
            }
        }
        
        [Browsable(false)]
        public VSLangProj.ProjectConfigurationProperties ActiveConfigurationSettings {
            get { return new OAProjectConfigurationProperties(Node.ProjectMgr); }
        }

        [Browsable(false)]
        public string ActiveFileSharePath {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public VSLangProj.prjWebAccessMethod ActiveWebAccessMethod {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string ApplicationIcon {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string AssemblyKeyContainerName {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string AssemblyName {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string AssemblyOriginatorKeyFile {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjOriginatorKeyMode AssemblyOriginatorKeyMode {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjScriptLanguage DefaultClientScript {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjHTMLPageLayout DefaultHTMLPageLayout {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string DefaultNamespace {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjTargetSchema DefaultTargetSchema {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public bool DelaySign {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public new object ExtenderNames {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string FileSharePath {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public bool LinkRepair {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string LocalPath {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string OfflineURL {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public VSLangProj.prjCompare OptionCompare {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjOptionExplicit OptionExplicit {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjOptionStrict OptionStrict {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string OutputFileName {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public VSLangProj.prjOutputType OutputType {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public VSLangProj.prjProjectType ProjectType {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string ReferencePath {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string ServerExtensionsVersion {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string StartupObject {
            get {
                return Node.ProjectMgr.GetProjectProperty(CommonConstants.StartupFile);
            }
            set {
                Node.ProjectMgr.SetProjectProperty(CommonConstants.StartupFile, value);
            }
        }

        [Browsable(false)]
        public string URL {
            get { return CommonUtils.MakeUri(Node.ProjectMgr.Url, false, UriKind.Absolute).AbsoluteUri;  }
        }

        [Browsable(false)]
        public VSLangProj.prjWebAccessMethod WebAccessMethod {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        [Browsable(false)]
        public string WebServer {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string WebServerVersion {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public string __id {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public object __project {
            get { throw new System.NotImplementedException(); }
        }

        [Browsable(false)]
        public object get_Extender(string ExtenderName) {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}

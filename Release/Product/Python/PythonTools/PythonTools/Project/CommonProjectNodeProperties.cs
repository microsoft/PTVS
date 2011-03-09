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

using System.ComponentModel;
using System.Runtime.InteropServices;


namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]    
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class CommonProjectNodeProperties : ProjectNodeProperties {

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
        /// Returns/Sets the WorkingDirectory project property
        /// </summary>
        [Browsable(false)]
        public string SearchPath {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.SearchPath, true);
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
        #endregion
    }
}

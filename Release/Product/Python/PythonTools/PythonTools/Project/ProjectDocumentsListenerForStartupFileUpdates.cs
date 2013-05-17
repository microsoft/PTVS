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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Updates the dynamic project property called StartupFile 
    /// </summary>
    internal class ProjectDocumentsListenerForStartupFileUpdates : ProjectDocumentsListener {
        #region fields
        /// <summary>
        /// The dynamic project who adviced for TrackProjectDocumentsEvents
        /// </summary>
        private CommonProjectNode _project;
        #endregion

        #region ctors
        public ProjectDocumentsListenerForStartupFileUpdates(ServiceProvider serviceProvider, CommonProjectNode project)
            : base(serviceProvider) {
            _project = project;
        }
        #endregion

        #region overriden methods
        public override int OnAfterRenameFiles(int cProjects, int cFiles, IVsProject[] projects, int[] firstIndices, string[] oldFileNames, string[] newFileNames, VSRENAMEFILEFLAGS[] flags) {
            if (!_project.IsRefreshing) {
                //Get the current value of the StartupFile Property
                string currentStartupFile = _project.GetProjectProperty(CommonConstants.StartupFile, true);
                string fullPathToStartupFile = CommonUtils.GetAbsoluteFilePath(_project.ProjectHome, currentStartupFile);

                //Investigate all of the oldFileNames if they are equal to the current StartupFile
                int index = 0;
                foreach (string oldfile in oldFileNames) {
                    //Compare the files and update the StartupFile Property if the currentStartupFile is an old file
                    if (CommonUtils.IsSamePath(oldfile, fullPathToStartupFile)) {
                        //Get the newfilename and update the StartupFile property
                        string newfilename = newFileNames[index];
                        CommonFileNode node = _project.FindNodeByFullPath(newfilename) as CommonFileNode;
                        if (node == null)
                            throw new InvalidOperationException("Could not find the CommonFileNode object");
                        //Startup file has been renamed
                        _project.SetProjectProperty(
                            CommonConstants.StartupFile,
                            CommonUtils.GetRelativeFilePath(_project.ProjectHome, node.Url));
                        break;
                    }
                    index++;
                }
            }
            return VSConstants.S_OK;
        }

        public override int OnAfterRemoveFiles(int cProjects, int cFiles, IVsProject[] projects, int[] firstIndices, string[] oldFileNames, VSREMOVEFILEFLAGS[] flags) {
            if (!_project.IsRefreshing) {
                //Get the current value of the StartupFile Property
                string currentStartupFile = _project.GetProjectProperty(CommonConstants.StartupFile, true);
                string fullPathToStartupFile = CommonUtils.GetAbsoluteFilePath(_project.ProjectHome, currentStartupFile);

                //Investigate all of the oldFileNames if they are equal to the current StartupFile
                int index = 0;
                foreach (string oldfile in oldFileNames) {
                    //Compare the files and update the StartupFile Property if the currentStartupFile is an old file
                    if (CommonUtils.IsSamePath(oldfile, fullPathToStartupFile)) {
                        //Startup file has been removed
                        _project.SetProjectProperty(CommonConstants.StartupFile, null);
                        break;
                    }
                    index++;
                }
            }
            return VSConstants.S_OK;
        }
        #endregion
    }
}

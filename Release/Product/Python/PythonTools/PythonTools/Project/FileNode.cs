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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.PythonTools.Project
{
    
    [ComVisible(true)]
    public class FileNode : HierarchyNode
    {
        private bool _isLinkFile;

        #region static fields
        private static Dictionary<string, int> extensionIcons;
        #endregion

        #region overriden Properties
        /// <summary>
        /// overwrites of the generic hierarchyitem.
        /// </summary>
        [System.ComponentModel.BrowsableAttribute(false)]
        public override string Caption
        {
            get
            {
                // Use LinkedIntoProjectAt property if available
                string caption = this.ItemNode.GetMetadata(ProjectFileConstants.LinkedIntoProjectAt);
                if(caption == null || caption.Length == 0)
                {
                    // Otherwise use filename
                    caption = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
                    caption = Path.GetFileName(caption);
                }
                return caption;
            }
        }

        public override string GetEditLabel() {
            if (IsLinkFile) {
                // cannot rename link files
                return null;
            }
            return Caption;
        }

        public override int ImageIndex
        {
            get
            {
                // Check if the file is there.
                if(!this.CanShowDefaultIcon())
                {
                    return (int)ProjectNode.ImageName.MissingFile;
                }

                //Check for known extensions
                int imageIndex;
                string extension = System.IO.Path.GetExtension(this.FileName);
                if((string.IsNullOrEmpty(extension)) || (!extensionIcons.TryGetValue(extension, out imageIndex)))
                {
                    // Missing or unknown extension; let the base class handle this case.
                    return base.ImageIndex;
                }

                // The file type is known and there is an image for it in the image list.
                return imageIndex;
            }
        }

        public override bool IsLinkFile
        {
            get 
            {
                return _isLinkFile;
            }
        }

        internal void SetIsLinkFile(bool value) 
        {
            _isLinkFile = value;
        }

        protected override VSOVERLAYICON OverlayIconIndex {
            get 
            {
                if (IsLinkFile) 
                {
                    return VSOVERLAYICON.OVERLAYICON_SHORTCUT;
                }
                return VSOVERLAYICON.OVERLAYICON_NONE;
            }
        }

        public override Guid ItemTypeGuid
        {
            get { return VSConstants.GUID_ItemType_PhysicalFile; }
        }

        public override int MenuCommandId
        {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        public override string Url
        {
            get
            {
                return GetAbsoluteUrlFromMsbuild();
            }
        }
        #endregion

        #region ctor
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static FileNode()
        {
            // Build the dictionary with the mapping between some well known extensions
            // and the index of the icons inside the standard image list.
            extensionIcons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            extensionIcons.Add(".aspx", (int)ProjectNode.ImageName.WebForm);
            extensionIcons.Add(".asax", (int)ProjectNode.ImageName.GlobalApplicationClass);
            extensionIcons.Add(".asmx", (int)ProjectNode.ImageName.WebService);
            extensionIcons.Add(".ascx", (int)ProjectNode.ImageName.WebUserControl);
            extensionIcons.Add(".asp", (int)ProjectNode.ImageName.ASPPage);
            extensionIcons.Add(".config", (int)ProjectNode.ImageName.WebConfig);
            extensionIcons.Add(".htm", (int)ProjectNode.ImageName.HTMLPage);
            extensionIcons.Add(".html", (int)ProjectNode.ImageName.HTMLPage);
            extensionIcons.Add(".css", (int)ProjectNode.ImageName.StyleSheet);
            extensionIcons.Add(".xsl", (int)ProjectNode.ImageName.StyleSheet);
            extensionIcons.Add(".vbs", (int)ProjectNode.ImageName.ScriptFile);
            extensionIcons.Add(".js", (int)ProjectNode.ImageName.ScriptFile);
            extensionIcons.Add(".wsf", (int)ProjectNode.ImageName.ScriptFile);
            extensionIcons.Add(".txt", (int)ProjectNode.ImageName.TextFile);
            extensionIcons.Add(".resx", (int)ProjectNode.ImageName.Resources);
            extensionIcons.Add(".rc", (int)ProjectNode.ImageName.Resources);
            extensionIcons.Add(".bmp", (int)ProjectNode.ImageName.Bitmap);
            extensionIcons.Add(".ico", (int)ProjectNode.ImageName.Icon);
            extensionIcons.Add(".gif", (int)ProjectNode.ImageName.Image);
            extensionIcons.Add(".jpg", (int)ProjectNode.ImageName.Image);
            extensionIcons.Add(".png", (int)ProjectNode.ImageName.Image);
            extensionIcons.Add(".map", (int)ProjectNode.ImageName.ImageMap);
            extensionIcons.Add(".wav", (int)ProjectNode.ImageName.Audio);
            extensionIcons.Add(".mid", (int)ProjectNode.ImageName.Audio);
            extensionIcons.Add(".midi", (int)ProjectNode.ImageName.Audio);
            extensionIcons.Add(".avi", (int)ProjectNode.ImageName.Video);
            extensionIcons.Add(".mov", (int)ProjectNode.ImageName.Video);
            extensionIcons.Add(".mpg", (int)ProjectNode.ImageName.Video);
            extensionIcons.Add(".mpeg", (int)ProjectNode.ImageName.Video);
            extensionIcons.Add(".cab", (int)ProjectNode.ImageName.CAB);
            extensionIcons.Add(".jar", (int)ProjectNode.ImageName.JAR);
            extensionIcons.Add(".xslt", (int)ProjectNode.ImageName.XSLTFile);
            extensionIcons.Add(".xsd", (int)ProjectNode.ImageName.XMLSchema);
            extensionIcons.Add(".xml", (int)ProjectNode.ImageName.XMLFile);
            extensionIcons.Add(".pfx", (int)ProjectNode.ImageName.PFX);
            extensionIcons.Add(".snk", (int)ProjectNode.ImageName.SNK);
        }

        /// <summary>
        /// Constructor for the FileNode
        /// </summary>
        /// <param name="root">Root of the hierarchy</param>
        /// <param name="e">Associated project element</param>
        public FileNode(ProjectNode root, MsBuildProjectElement element)
            : base(root, element)
        {
            if(this.ProjectMgr.NodeHasDesigner(this.ItemNode.GetMetadata(ProjectFileConstants.Include)))
            {
                this.HasDesigner = true;
            }
        }
        #endregion

        #region overridden methods
        protected override NodeProperties CreatePropertiesObject()
        {
            if (IsLinkFile) {
                return new LinkFileNodeProperties(this);
            }
            return new FileNodeProperties(this);
        }

        public override object GetIconHandle(bool open)
        {
            int index = this.ImageIndex;
            if(NoImage == index)
            {
                // There is no image for this file; let the base class handle this case.
                return base.GetIconHandle(open);
            }
            // Return the handle for the image.
            return this.ProjectMgr.ImageHandler.GetIconHandle(index);
        }

        /// <summary>
        /// Get an instance of the automation object for a FileNode
        /// </summary>
        /// <returns>An instance of the Automation.OAFileNode if succeeded</returns>
        public override object GetAutomationObject()
        {
            if(this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return null;
            }

            return new Automation.OAFileItem(this.ProjectMgr.GetAutomationObject() as Automation.OAProject, this);
        }

        /// <summary>
        /// Renames a file node.
        /// </summary>
        /// <param name="label">The new name.</param>
        /// <returns>An errorcode for failure or S_OK.</returns>
        /// <exception cref="InvalidOperationException" if the file cannot be validated>
        /// <devremark> 
        /// We are going to throw instaed of showing messageboxes, since this method is called from various places where a dialog box does not make sense.
        /// For example the FileNodeProperties are also calling this method. That should not show directly a messagebox.
        /// Also the automation methods are also calling SetEditLabel
        /// </devremark>

        public override int SetEditLabel(string label)
        {
            // IMPORTANT NOTE: This code will be called when a parent folder is renamed. As such, it is
            //                 expected that we can be called with a label which is the same as the current
            //                 label and this should not be considered a NO-OP.
            if(this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return VSConstants.E_FAIL;
            }

            // Validate the filename. 
            if(String.IsNullOrEmpty(label))
            {
                throw new InvalidOperationException(String.Format(SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture), label));
            }
            else if(label.Length > NativeMethods.MAX_PATH)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.PathTooLong, CultureInfo.CurrentUICulture), label));
            }
            else if(Utilities.IsFileNameInvalid(label))
            {
                throw new InvalidOperationException(String.Format(SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture), label));
            }

            for(HierarchyNode n = this.Parent.FirstChild; n != null; n = n.NextSibling)
            {
                if(n != this && String.Compare(n.Caption, label, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    //A file or folder with the name '{0}' already exists on disk at this location. Please choose another name.
                    //If this file or folder does not appear in the Solution Explorer, then it is not currently part of your project. To view files which exist on disk, but are not in the project, select Show All Files from the Project menu.
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileOrFolderAlreadyExists, CultureInfo.CurrentUICulture), label));
                }
            }

            string fileName = Path.GetFileNameWithoutExtension(label);

            // Verify that the file extension is unchanged
            string strRelPath = Path.GetFileName(this.ItemNode.GetMetadata(ProjectFileConstants.Include));
            if(String.Compare(Path.GetExtension(strRelPath), Path.GetExtension(label), StringComparison.OrdinalIgnoreCase) != 0 &&
                !IsToAndFromValidPythonExtension(label, strRelPath))
            {
                // Prompt to confirm that they really want to change the extension of the file
                string message = SR.GetString(SR.ConfirmExtensionChange, CultureInfo.CurrentUICulture, new string[] { label });
                IVsUIShell shell = this.ProjectMgr.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;

                Debug.Assert(shell != null, "Could not get the ui shell from the project");
                Utilities.CheckNotNull(shell);

                if(!VsShellUtilities.PromptYesNo(message, null, OLEMSGICON.OLEMSGICON_INFO, shell))
                {
                    // The user cancelled the confirmation for changing the extension.
                    // Return S_OK in order not to show any extra dialog box
                    return VSConstants.S_OK;
                }
            }


            // Build the relative path by looking at folder names above us as one scenarios
            // where we get called is when a folder above us gets renamed (in which case our path is invalid)
            HierarchyNode parent = this.Parent;
            while(parent != null && (parent is FolderNode))
            {
                strRelPath = Path.Combine(parent.Caption, strRelPath);
                parent = parent.Parent;
            }

            return SetEditLabel(label, strRelPath);
        }

        /// <summary>
        /// Checks if both the to and from file extensions are valid Python code file extensions.
        /// </summary>
        private static bool IsToAndFromValidPythonExtension(string label, string strRelPath) {
            return ((Path.GetExtension(strRelPath).Equals(".py", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(label).Equals(".pyw", StringComparison.OrdinalIgnoreCase)) ||
                (Path.GetExtension(strRelPath).Equals(".pyw", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(label).Equals(".py", StringComparison.OrdinalIgnoreCase))
                );
        }

        public override string GetMkDocument()
        {
            Debug.Assert(this.Url != null, "No url sepcified for this node");

            return this.Url;
        }

        /// <summary>
        /// Delete the item corresponding to the specified path from storage.
        /// </summary>
        /// <param name="path"></param>
        protected internal override void DeleteFromStorage(string path)
        {
            if(File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal); // make sure it's not readonly.
                File.Delete(path);
            }
        }

        [System.ComponentModel.BrowsableAttribute(false)]
        internal new MsBuildProjectElement ItemNode {
            get {
                return (MsBuildProjectElement)base.ItemNode;
            }
            set {
                base.ItemNode = value;
            }
        }

        /// <summary>
        /// Rename the underlying document based on the change the user just made to the edit label.
        /// </summary>
        protected internal override int SetEditLabel(string label, string relativePath)
        {
            int returnValue = VSConstants.S_OK;
            uint oldId = this.ID;
            string strSavePath = Path.GetDirectoryName(relativePath);

            if(!Path.IsPathRooted(relativePath))
            {
                strSavePath = Path.Combine(Path.GetDirectoryName(this.ProjectMgr.BaseURI.Uri.LocalPath), strSavePath);
            }

            string newName = Path.Combine(strSavePath, label);

            if(NativeMethods.IsSamePath(newName, this.Url))
            {
                // If this is really a no-op, then nothing to do
                if(String.Compare(newName, this.Url, StringComparison.Ordinal) == 0)
                    return VSConstants.S_FALSE;
            }
            else
            {
                // If the renamed file already exists then quit (unless it is the result of the parent having done the move).
                if(IsFileOnDisk(newName)
                    && (IsFileOnDisk(this.Url)
                    || String.Compare(Path.GetFileName(newName), Path.GetFileName(this.Url), StringComparison.Ordinal) != 0))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileCannotBeRenamedToAnExistingFile, CultureInfo.CurrentUICulture), label));
                }
                else if(newName.Length > NativeMethods.MAX_PATH)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.PathTooLong, CultureInfo.CurrentUICulture), label));
                }

            }

            string oldName = this.Url;
            // must update the caption prior to calling RenameDocument, since it may
            // cause queries of that property (such as from open editors).
            string oldrelPath = this.ItemNode.GetMetadata(ProjectFileConstants.Include);

            try
            {
                if(!RenameDocument(oldName, newName))
                {
                    this.ItemNode.Rename(oldrelPath);
                    this.ItemNode.RefreshProperties();
                }

                if(this is DependentFileNode)
                {
                    OnInvalidateItems(this.Parent);
                }

            }
            catch(Exception e)
            {
                // Just re-throw the exception so we don't get duplicate message boxes.
                Trace.WriteLine("Exception : " + e.Message);
                this.RecoverFromRenameFailure(newName, oldrelPath);
                returnValue = Marshal.GetHRForException(e);
                throw;
            }
            // Return S_FALSE if the hierarchy item id has changed.  This forces VS to flush the stale
            // hierarchy item id.
            if(returnValue == (int)VSConstants.S_OK || returnValue == (int)VSConstants.S_FALSE || returnValue == VSConstants.OLE_E_PROMPTSAVECANCELLED)
            {
                return (oldId == this.ID) ? VSConstants.S_OK : (int)VSConstants.S_FALSE;
            }

            return returnValue;
        }

        /// <summary>
        /// Returns a specific Document manager to handle files
        /// </summary>
        /// <returns>Document manager object</returns>
        protected internal override DocumentManager GetDocumentManager()
        {
            return new FileDocumentManager(this);
        }

        /// <summary>
        /// Called by the drag&drop implementation to ask the node
        /// which is being dragged/droped over which nodes should
        /// process the operation.
        /// This allows for dragging to a node that cannot contain
        /// items to let its parent accept the drop, while a reference
        /// node delegate to the project and a folder/project node to itself.
        /// </summary>
        /// <returns></returns>
        protected internal override HierarchyNode GetDragTargetHandlerNode()
        {
            Debug.Assert(this.ProjectMgr != null, " The project manager is null for the filenode");
            HierarchyNode handlerNode = this;
            while(handlerNode != null && !(handlerNode is ProjectNode || handlerNode is FolderNode))
                handlerNode = handlerNode.Parent;
            if(handlerNode == null)
                handlerNode = this.ProjectMgr;
            return handlerNode;
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if(this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }

            // Exec on special filenode commands
            if(cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                IVsWindowFrame windowFrame = null;

                switch((VsCommands)cmd)
                {
                    case VsCommands.ViewCode:
                        return ((FileDocumentManager)this.GetDocumentManager()).Open(false, false, VSConstants.LOGVIEWID_Code, out windowFrame, WindowFrameShowAction.Show);

                    case VsCommands.ViewForm:
                        return ((FileDocumentManager)this.GetDocumentManager()).Open(false, false, VSConstants.LOGVIEWID_Designer, out windowFrame, WindowFrameShowAction.Show);

                    case VsCommands.Open:
                        return ((FileDocumentManager)this.GetDocumentManager()).Open(false, false, WindowFrameShowAction.Show);

                    case VsCommands.OpenWith:
                        return ((FileDocumentManager)this.GetDocumentManager()).Open(false, true, VSConstants.LOGVIEWID_UserChooseView, out windowFrame, WindowFrameShowAction.Show);
                }
            }

            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }


        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
        {
            if(cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch((VsCommands)cmd)
                {
                    case VsCommands.Copy:
                    case VsCommands.Paste:
                    case VsCommands.Cut:
                    case VsCommands.Rename:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands.ViewCode:
                    //case VsCommands.Delete: goto case VsCommands.OpenWith;
                    case VsCommands.Open:
                    case VsCommands.OpenWith:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else if(cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                if((VsCommands2K)cmd == VsCommands2K.EXCLUDEFROMPROJECT)
                {
                    result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                    return VSConstants.S_OK;
                }
            }
            else
            {
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }


        protected override void DoDefaultAction()
        {
            FileDocumentManager manager = this.GetDocumentManager() as FileDocumentManager;
            Debug.Assert(manager != null, "Could not get the FileDocumentManager");
            manager.Open(false, false, WindowFrameShowAction.Show);
        }

        /// <summary>
        /// Performs a SaveAs operation of an open document. Called from SaveItem after the running document table has been updated with the new doc data.
        /// </summary>
        /// <param name="docData">A pointer to the document in the rdt</param>
        /// <param name="newFilePath">The new file path to the document</param>
        /// <returns></returns>
        protected override int AfterSaveItemAs(IntPtr docData, string newFilePath)
        {
            Utilities.ArgumentNotNullOrEmpty("newFilePath", newFilePath);

            int returnCode = VSConstants.S_OK;
            newFilePath = newFilePath.Trim();

            //Identify if Path or FileName are the same for old and new file
            string newDirectoryName = Path.GetDirectoryName(newFilePath);
            Uri newDirectoryUri = new Uri(newDirectoryName);
            string newCanonicalDirectoryName = newDirectoryUri.LocalPath;
            newCanonicalDirectoryName = newCanonicalDirectoryName.TrimEnd(Path.DirectorySeparatorChar);
            string oldCanonicalDirectoryName = new Uri(Path.GetDirectoryName(this.GetMkDocument())).LocalPath;
            oldCanonicalDirectoryName = oldCanonicalDirectoryName.TrimEnd(Path.DirectorySeparatorChar);
            string errorMessage = String.Empty;
            bool isSamePath = NativeMethods.IsSamePath(newCanonicalDirectoryName, oldCanonicalDirectoryName);
            bool isSameFile = NativeMethods.IsSamePath(newFilePath, this.Url);

            // Currently we do not support if the new directory is located outside the project cone
            string projectCannonicalDirecoryName = new Uri(this.ProjectMgr.ProjectFolder).LocalPath;
            projectCannonicalDirecoryName = projectCannonicalDirecoryName.TrimEnd(Path.DirectorySeparatorChar);
            //Get target container
            HierarchyNode targetContainer = null;
            bool isLink = false;
            if(!isSamePath && newCanonicalDirectoryName.IndexOf(projectCannonicalDirecoryName, StringComparison.OrdinalIgnoreCase) == -1)
            {
                targetContainer = this.Parent;
                isLink = true;
            } else if(isSamePath)
            {
                targetContainer = this.Parent;
            }
            else if(NativeMethods.IsSamePath(newCanonicalDirectoryName, projectCannonicalDirecoryName))
            {
                //the projectnode is the target container
                targetContainer = this.ProjectMgr;
            }
            else
            {
                //search for the target container among existing child nodes
                targetContainer = this.ProjectMgr.FindChild(newDirectoryName);
                if(targetContainer != null && (targetContainer is FileNode))
                {
                    // We already have a file node with this name in the hierarchy.
                    errorMessage = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileAlreadyExistsAndCannotBeRenamed, CultureInfo.CurrentUICulture), Path.GetFileNameWithoutExtension(newFilePath));
                    throw new InvalidOperationException(errorMessage);
                }
            }

            if(targetContainer == null)
            {
                // Add a chain of subdirectories to the project.
                string relativeUri = PackageUtilities.GetPathDistance(this.ProjectMgr.BaseURI.Uri, newDirectoryUri);
                Debug.Assert(!String.IsNullOrEmpty(relativeUri) && relativeUri != newDirectoryUri.LocalPath, "Could not make pat distance of " + this.ProjectMgr.BaseURI.Uri.LocalPath + " and " + newDirectoryUri);
                targetContainer = this.ProjectMgr.CreateFolderNodes(relativeUri);
            }
            Debug.Assert(targetContainer != null, "We should have found a target node by now");

            //Suspend file changes while we rename the document
            string oldrelPath = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
            string oldName = Path.Combine(this.ProjectMgr.ProjectFolder, oldrelPath);
            SuspendFileChanges sfc = new SuspendFileChanges(this.ProjectMgr.Site, oldName);
            sfc.Suspend();

            try
            {
                // Rename the node.	
                DocumentManager.UpdateCaption(this.ProjectMgr.Site, Path.GetFileName(newFilePath), docData);
                // Check if the file name was actually changed.
                // In same cases (e.g. if the item is a file and the user has changed its encoding) this function
                // is called even if there is no real rename.
                if(!isSameFile || (this.Parent.ID != targetContainer.ID))
                {
                    // The path of the file is changed or its parent is changed; in both cases we have
                    // to rename the item.
                    if (isLink != IsLinkFile) {
                        if (isLink) {
                            var newPath = CommonUtils.CreateFriendlyFilePath(
                                this.ProjectMgr.ProjectFolder,
                                Path.Combine(Path.GetDirectoryName(Url), Path.GetFileName(newFilePath))
                            );

                            ItemNode.SetMetadata(ProjectFileConstants.Link, newPath);
                        } else {
                            ItemNode.SetMetadata(ProjectFileConstants.Link, null);
                        }
                        SetIsLinkFile(isLink);
                    }

                    this.RenameFileNode(oldName, newFilePath, targetContainer.ID);
                    OnInvalidateItems(this.Parent);
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine("Exception : " + e.Message);
                this.RecoverFromRenameFailure(newFilePath, oldrelPath);
                throw;
            }
            finally
            {
                sfc.Resume();
            }

            return returnCode;
        }

        /// <summary>
        /// Determines if this is node a valid node for painting the default file icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon()
        {
            string moniker = this.GetMkDocument();

            if(String.IsNullOrEmpty(moniker) || !File.Exists(moniker))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region virtual methods
        public virtual string FileName
        {
            get
            {
                return this.Caption;
            }
            set
            {
                this.SetEditLabel(value);
            }
        }

        /// <summary>
        /// Determine if this item is represented physical on disk and shows a messagebox in case that the file is not present and a UI is to be presented.
        /// </summary>
        /// <param name="showMessage">true if user should be presented for UI in case the file is not present</param>
        /// <returns>true if file is on disk</returns>
        internal protected virtual bool IsFileOnDisk(bool showMessage)
        {
            bool fileExist = IsFileOnDisk(this.Url);

            if(!fileExist && showMessage && !Utilities.IsInAutomationFunction(this.ProjectMgr.Site))
            {
                string message = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ItemDoesNotExistInProjectDirectory, CultureInfo.CurrentUICulture), this.Caption);
                string title = string.Empty;
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
            }

            return fileExist;
        }

        /// <summary>
        /// Determine if the file represented by "path" exist in storage.
        /// Override this method if your files are not persisted on disk.
        /// </summary>
        /// <param name="path">Url representing the file</param>
        /// <returns>True if the file exist</returns>
        internal protected virtual bool IsFileOnDisk(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Renames the file in the hierarchy by removing old node and adding a new node in the hierarchy.
        /// </summary>
        /// <param name="oldFileName">The old file name.</param>
        /// <param name="newFileName">The new file name</param>
        /// <param name="newParentId">The new parent id of the item.</param>
        /// <returns>The newly added FileNode.</returns>
        /// <remarks>While a new node will be used to represent the item, the underlying MSBuild item will be the same and as a result file properties saved in the project file will not be lost.</remarks>
        protected virtual FileNode RenameFileNode(string oldFileName, string newFileName, uint newParentId)
        {
            if(string.Compare(oldFileName, newFileName, StringComparison.Ordinal) == 0)
            {
                // We do not want to rename the same file
                return null;
            }

            this.OnItemDeleted();
            this.Parent.RemoveChild(this);
            this.ID = this.ProjectMgr.ItemIdMap.Add(this);
            this.ItemNode.Item.Xml.Include = CommonUtils.CreateFriendlyFilePath(ProjectMgr.BaseURI.Uri.LocalPath, newFileName);
            this.ItemNode.RefreshProperties();
            this.ProjectMgr.SetProjectFileDirty(true);
            HierarchyNode newParent;
            if (newParentId == VSConstants.VSITEMID_ROOT) {
                newParent = ProjectMgr;
            } else {
                newParent = ((HierarchyNode)this.ProjectMgr.ItemIdMap[newParentId]);
            }
            newParent.AddChild(this);
            this.Parent = newParent;

            this.ReDraw(UIHierarchyElement.Caption);

            //Update the new document in the RDT.
            DocumentManager.RenameDocument(this.ProjectMgr.Site, oldFileName, newFileName, ID);

            //Select the new node in the hierarchy
            IVsUIHierarchyWindow uiWindow = UIHierarchyUtilities.GetUIHierarchyWindow(this.ProjectMgr.Site, SolutionExplorer);
            ErrorHandler.ThrowOnFailure(uiWindow.ExpandItem(this.ProjectMgr, this.ID, EXPANDFLAGS.EXPF_SelectItem));

            RenameChildNodes(this);

            return this;
        }

        /// <summary>
        /// Rename all childnodes
        /// </summary>
        /// <param name="newFileNode">The newly added Parent node.</param>
        protected virtual void RenameChildNodes(FileNode parentNode)
        {
            foreach(HierarchyNode child in GetChildNodes())
            {
                FileNode childNode = child as FileNode;
                if(null == childNode)
                {
                    continue;
                }
                string newfilename;
                if(childNode.HasParentNodeNameRelation)
                {
                    string relationalName = childNode.Parent.GetRelationalName();
                    string extension = childNode.GetRelationNameExtension();
                    newfilename = relationalName + extension;
                    newfilename = Path.Combine(Path.GetDirectoryName(childNode.Parent.GetMkDocument()), newfilename);
                }
                else
                {
                    newfilename = Path.Combine(Path.GetDirectoryName(childNode.Parent.GetMkDocument()), childNode.Caption);
                }

                childNode.RenameDocument(childNode.GetMkDocument(), newfilename);

                //We must update the DependsUpon property since the rename operation will not do it if the childNode is not renamed
                //which happens if the is no name relation between the parent and the child
                string dependentOf = childNode.ItemNode.GetMetadata(ProjectFileConstants.DependentUpon);
                if(!string.IsNullOrEmpty(dependentOf))
                {
                    childNode.ItemNode.SetMetadata(ProjectFileConstants.DependentUpon, childNode.Parent.ItemNode.GetMetadata(ProjectFileConstants.Include));
                }
            }
        }


        /// <summary>
        /// Tries recovering from a rename failure.
        /// </summary>
        /// <param name="fileThatFailed"> The file that failed to be renamed.</param>
        /// <param name="originalFileName">The original filenamee</param>
        protected virtual void RecoverFromRenameFailure(string fileThatFailed, string originalFileName)
        {
            if(this.ItemNode != null && !String.IsNullOrEmpty(originalFileName))
            {
                this.ItemNode.Rename(originalFileName);
            }
        }

        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            if(deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage)
            {
                return this.ProjectMgr.CanProjectDeleteItems;
            }
            return false;
        }

        /// <summary>
        /// This should be overriden for node that are not saved on disk
        /// </summary>
        /// <param name="oldName">Previous name in storage</param>
        /// <param name="newName">New name in storage</param>
        protected virtual void RenameInStorage(string oldName, string newName)
        {
            File.Move(oldName, newName);
        }

        /// <summary>
        /// This method should be overridden to provide the list of special files and associated flags for source control.
        /// </summary>
        /// <param name="sccFile">One of the file associated to the node.</param>
        /// <param name="files">The list of files to be placed under source control.</param>
        /// <param name="flags">The flags that are associated to the files.</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "scc")]
        protected internal override void GetSccSpecialFiles(string sccFile, IList<string> files, IList<tagVsSccFilesFlags> flags)
        {
            if(this.ExcludeNodeFromScc)
            {
                return;
            }

            Utilities.ArgumentNotNull("files", files);
            Utilities.ArgumentNotNull("flags", flags);

            foreach(HierarchyNode node in this.GetChildNodes())
            {
                files.Add(node.GetMkDocument());
            }
        }

        #endregion

        #region Helper methods
        /// <summary>
        /// Get's called to rename the eventually running document this hierarchyitem points to
        /// </summary>
        /// returns FALSE if the doc can not be renamed
        internal bool RenameDocument(string oldName, string newName)
        {
            IVsRunningDocumentTable pRDT = this.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if(pRDT == null) return false;
            IntPtr docData = IntPtr.Zero;
            IVsHierarchy pIVsHierarchy;
            uint itemId;
            uint uiVsDocCookie;

            SuspendFileChanges sfc = null;

            if (File.Exists(oldName)) {
                sfc = new SuspendFileChanges(this.ProjectMgr.Site, oldName);
                sfc.Suspend();
            }

            try
            {
                // Suspend ms build since during a rename operation no msbuild re-evaluation should be performed until we have finished.
                // Scenario that could fail if we do not suspend.
                // We have a project system relying on MPF that triggers a Compile target build (re-evaluates itself) whenever the project changes. (example: a file is added, property changed.)
                // 1. User renames a file in  the above project sytem relying on MPF
                // 2. Our rename funstionality implemented in this method removes and readds the file and as a post step copies all msbuild entries from the removed file to the added file.
                // 3. The project system mentioned will trigger an msbuild re-evaluate with the new item, because it was listening to OnItemAdded. 
                //    The problem is that the item at the "add" time is only partly added to the project, since the msbuild part has not yet been copied over as mentioned in part 2 of the last step of the rename process.
                //    The result is that the project re-evaluates itself wrongly.
                VSRENAMEFILEFLAGS renameflag = VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags;
                ErrorHandler.ThrowOnFailure(pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, oldName, out pIVsHierarchy, out itemId, out docData, out uiVsDocCookie));

                if(pIVsHierarchy != null && !Utilities.IsSameComObject(pIVsHierarchy, this.ProjectMgr))
                {
                    // Don't rename it if it wasn't opened by us.
                    return false;
                }

                // ask other potentially running packages
                if(!this.ProjectMgr.Tracker.CanRenameItem(oldName, newName, renameflag))
                {
                    return false;
                }
                // Allow the user to "fix" the project by renaming the item in the hierarchy
                // to the real name of the file on disk.
                if(IsFileOnDisk(oldName) || !IsFileOnDisk(newName))
                {
                    RenameInStorage(oldName, newName);
                }

                string newFileName = Path.GetFileName(newName);
                    
                bool caseOnlyChange = NativeMethods.IsSamePath(oldName, newName);
                if(!caseOnlyChange)
                {
                    // Check out the project file if necessary.
                    if(!this.ProjectMgr.QueryEditProjectFile(false))
                    {
                        throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                    }

                    this.RenameFileNode(oldName, newName);
                }
                else
                {
                    this.RenameCaseOnlyChange(newFileName);
                }

                DocumentManager.UpdateCaption(this.ProjectMgr.Site, Caption, docData);                

                // changed from MPFProj:
                // http://mpfproj10.codeplex.com/WorkItem/View.aspx?WorkItemId=8231
                this.ProjectMgr.Tracker.OnItemRenamed(oldName, newName, renameflag);
            }
            finally
            {
                if (sfc != null) {
                    sfc.Resume();
                }
                if(docData != IntPtr.Zero)
                {
                    Marshal.Release(docData);
                }
            }

            return true;
        }

        private FileNode RenameFileNode(string oldFileName, string newFileName)
        {
            return this.RenameFileNode(oldFileName, newFileName, this.Parent.ID);
        }

        /// <summary>
        /// Renames the file node for a case only change.
        /// </summary>
        /// <param name="newFileName">The new file name.</param>
        private void RenameCaseOnlyChange(string newFileName)
        {
            //Update the include for this item.
            string include = this.ItemNode.Item.EvaluatedInclude;
            if(String.Compare(include, newFileName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.ItemNode.Item.Xml.Include = newFileName;
            }
            else
            {
                string includeDir = Path.GetDirectoryName(include);
                this.ItemNode.Item.Xml.Include = Path.Combine(includeDir, newFileName);
            }

            this.ItemNode.RefreshProperties();

            this.ReDraw(UIHierarchyElement.Caption);
            this.RenameChildNodes(this);

            // Refresh the property browser.
            IVsUIShell shell = this.ProjectMgr.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
            Debug.Assert(shell != null, "Could not get the ui shell from the project");
            Utilities.CheckNotNull(shell);

            ErrorHandler.ThrowOnFailure(shell.RefreshPropertyBrowser(0));

            //Select the new node in the hierarchy
            IVsUIHierarchyWindow uiWindow = UIHierarchyUtilities.GetUIHierarchyWindow(this.ProjectMgr.Site, SolutionExplorer);
            ErrorHandler.ThrowOnFailure(uiWindow.ExpandItem(this.ProjectMgr, this.ID, EXPANDFLAGS.EXPF_SelectItem));
        }

        #endregion

        #region helpers
        

        /// <summary>
        /// Update the ChildNodes after the parent node has been renamed
        /// </summary>
        /// <param name="newFileNode">The new FileNode created as part of the rename of this node</param>
        private void SetNewParentOnChildNodes(FileNode newFileNode)
        {
            foreach(HierarchyNode childNode in GetChildNodes())
            {
                childNode.Parent = newFileNode;
            }
        }

        private List<HierarchyNode> GetChildNodes()
        {
            List<HierarchyNode> childNodes = new List<HierarchyNode>();
            HierarchyNode childNode = this.FirstChild;
            while(childNode != null)
            {
                childNodes.Add(childNode);
                childNode = childNode.NextSibling;
            }
            return childNodes;
        }
        #endregion
    }
}

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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.PythonTools.Project {
    public class CommonFileNode : FileNode {
        private OAVSProjectItem _vsProjectItem;
        private CommonProjectNode _project;

        public CommonFileNode(CommonProjectNode root, MsBuildProjectElement e)
            : base(root, e) {
            _project = root;
        }

        #region properties
        /// <summary>
        /// Returns bool indicating whether this node is of subtype "Form"
        /// </summary>
        public bool IsFormSubType {
            get {
                string result = this.ItemNode.GetMetadata(ProjectFileConstants.SubType);
                if (!String.IsNullOrEmpty(result) && string.Compare(result, ProjectFileAttributeValue.Form, true, CultureInfo.InvariantCulture) == 0)
                    return true;
                else
                    return false;
            }
        }
        /// <summary>
        /// Returns the SubType of a dynamic FileNode. It is 
        /// </summary>
        public string SubType {
            get {
                return this.ItemNode.GetMetadata(ProjectFileConstants.SubType);
            }
            set {
                this.ItemNode.SetMetadata(ProjectFileConstants.SubType, value);
            }
        }

        protected internal VSLangProj.VSProjectItem VSProjectItem {
            get {
                if (null == _vsProjectItem) {
                    _vsProjectItem = new OAVSProjectItem(this);
                }
                return _vsProjectItem;
            }
        }

#if DEV11
        public override __VSPROVISIONALVIEWINGSTATUS ProvisionalViewingStatus {
            get {
                return __VSPROVISIONALVIEWINGSTATUS.PVS_Enabled;
            }
        }
#endif

        #endregion

        #region overridden properties

        internal override object Object {
            get {
                return this.VSProjectItem;
            }
        }
        #endregion

        #region overridden methods

        public override int ImageIndex {
            get {
                if (!File.Exists(Url)) {
                    return (int)ProjectNode.ImageName.MissingFile;
                } else if (IsFormSubType) {
                    return (int)ProjectNode.ImageName.WindowsForm;
                } else if (this._project.IsCodeFile(FileName)) {
                    if (CommonUtils.IsSamePath(this.Url, _project.GetStartupFile())) {
                        return CommonProjectNode.ImageOffset + (int)CommonImageName.StartupFile;
                    } else {
                        return CommonProjectNode.ImageOffset + (int)CommonImageName.File;
                    }
                }
                return base.ImageIndex;
            }
        }

        /// <summary>
        /// Open a file depending on the SubType property associated with the file item in the project file
        /// </summary>
        protected override void DoDefaultAction() {
            FileDocumentManager manager = this.GetDocumentManager() as FileDocumentManager;
            Debug.Assert(manager != null, "Could not get the FileDocumentManager");

            Guid viewGuid =
                (IsFormSubType ? VSConstants.LOGVIEWID_Designer : VSConstants.LOGVIEWID_Code);
            IVsWindowFrame frame;
            manager.Open(false, false, viewGuid, out frame, WindowFrameShowAction.Show);
        }

        private static Guid CLSID_VsTextBuffer = new Guid("{8E7B96A8-E33D-11d0-A6D5-00C04FB67F6A}");

        /// <summary>
        /// Gets the text buffer for the file opening the document if necessary.
        /// </summary>
        public ITextBuffer GetTextBuffer() {
            if (UIThread.Instance.IsUIThread) {
                // http://pytools.codeplex.com/workitem/672
                // When we FindAndLockDocument we marshal on the main UI thread, and the docdata we get
                // back is marshalled back so that we'll marshal any calls on it back.  When we pass it
                // into IVsEditorAdaptersFactoryService we don't go through a COM boundary (it's a managed
                // call) and we therefore don't get the marshaled value, and it doesn't know what we're
                // talking about.  So run the whole operation on the UI thread.
                return GetTextBufferOnUIThread();
            }

            ITextBuffer res = null;
            UIThread.Instance.RunSync(
                () => {
                    res = GetTextBufferOnUIThread();
                }
            );
            return res;
        }

        private ITextBuffer GetTextBufferOnUIThread() {
            IVsTextManager textMgr = (IVsTextManager)GetService(typeof(SVsTextManager));
            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            var adapter = model.GetService<IVsEditorAdaptersFactoryService>();
            uint itemid;

            IVsRunningDocumentTable rdt = ProjectMgr.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (rdt != null) {
                IVsHierarchy hier;
                IVsPersistDocData persistDocData;
                uint cookie;
                bool docInRdt = true;
                IntPtr docData = IntPtr.Zero;
                int hr = NativeMethods.E_FAIL;
                try {
                    //Getting a read lock on the document. Must be released later.
                    hr = rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_ReadLock, GetMkDocument(), out hier, out itemid, out docData, out cookie);
                    if (ErrorHandler.Failed(hr) || docData == IntPtr.Zero) {
                        Guid iid = VSConstants.IID_IUnknown;
                        cookie = 0;
                        docInRdt = false;
                        ILocalRegistry localReg = this.ProjectMgr.GetService(typeof(SLocalRegistry)) as ILocalRegistry;
                        ErrorHandler.ThrowOnFailure(localReg.CreateInstance(CLSID_VsTextBuffer, null, ref iid, (uint)CLSCTX.CLSCTX_INPROC_SERVER, out docData));
                    }
                    persistDocData = Marshal.GetObjectForIUnknown(docData) as IVsPersistDocData;
                } finally {
                    if (docData != IntPtr.Zero) {
                        Marshal.Release(docData);
                    }
                }

                //Try to get the Text lines
                IVsTextLines srpTextLines = persistDocData as IVsTextLines;
                if (srpTextLines == null) {
                    // Try getting a text buffer provider first
                    IVsTextBufferProvider srpTextBufferProvider = persistDocData as IVsTextBufferProvider;
                    if (srpTextBufferProvider != null) {
                        hr = srpTextBufferProvider.GetTextBuffer(out srpTextLines);
                    }
                }

                // Unlock the document in the RDT if necessary
                if (docInRdt && rdt != null) {
                    ErrorHandler.ThrowOnFailure(rdt.UnlockDocument((uint)(_VSRDTFLAGS.RDT_ReadLock | _VSRDTFLAGS.RDT_Unlock_NoSave), cookie));
                }

                if (srpTextLines != null) {
                    return adapter.GetDocumentBuffer(srpTextLines);
                }
            }

            IWpfTextView view = GetTextView();

            return view.TextBuffer;
        }

        public IWpfTextView GetTextView() {
            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            var adapter = model.GetService<IVsEditorAdaptersFactoryService>();

            IVsTextView viewAdapter;
            uint itemid;
            IVsUIShellOpenDocument uiShellOpenDocument = GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            IVsUIHierarchy hierarchy;
            IVsWindowFrame pWindowFrame;

            VsShellUtilities.OpenDocument(
                ProjectMgr.Site,
                this.GetMkDocument(),
                Guid.Empty,
                out hierarchy,
                out itemid,
                out pWindowFrame,
                out viewAdapter);

            ErrorHandler.ThrowOnFailure(pWindowFrame.Show());
            return adapter.GetWpfTextView(viewAdapter);
        }

        /// <summary>
        /// Handles the menuitems
        /// </summary>
        protected override int QueryStatusOnNode(Guid guidCmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (guidCmdGroup == Microsoft.VisualStudio.Shell.VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.RUNCUSTOMTOOL:
                        result |= QueryStatusResult.NOTSUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            } 

            return base.QueryStatusOnNode(guidCmdGroup, cmd, pCmdText, ref result);
        }

        /// <summary>
        /// Common File Node can only be deleted from file system.
        /// </summary>        
        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            if (IsLinkFile) {
                // we don't delete link items, we only remove them from the project.  If we were
                // to return true when queried for both delete from storage and remove from project
                // the user would be prompted about which they would like to do.
                return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
            }
            return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage;
        }
        #endregion

        #region methods

        internal OleServiceProvider.ServiceCreatorCallback ServiceCreator {
            get { return new OleServiceProvider.ServiceCreatorCallback(this.CreateServices); }
        }

        protected virtual object CreateServices(Type serviceType) {
            object service = null;
            if (typeof(EnvDTE.ProjectItem) == serviceType) {
                service = GetAutomationObject();
            }
            return service;
        }

        #endregion
    }
}

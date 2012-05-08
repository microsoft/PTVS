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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Django.Project {
    class DjangoProject : FlavoredProject {
        internal DjangoPackage _package;
        private IVsProjectFlavorCfgProvider _innerVsProjectFlavorCfgProvider;
        private static Guid PythonProjectGuid = new Guid("888888a0-9f3d-457c-b088-3a5042f75d52");

        #region IVsAggregatableProject

        /// <summary>
        /// Do the initialization here (such as loading flavor specific
        /// information from the project)
        /// </summary>      
        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel) {
            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);

            // Load the icon we will be using for our nodes
            /*Assembly assembly = Assembly.GetExecutingAssembly();
            nodeIcon = new Icon(assembly.GetManifestResourceStream("Microsoft.VisualStudio.VSIP.Samples.Flavor.Node.ico"));
            this.FileAdded += new EventHandler<ProjectDocumentsChangeEventArgs>(this.UpdateIcons);
            this.FileRenamed += new EventHandler<ProjectDocumentsChangeEventArgs>(this.UpdateIcons);*/
        }

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.PreviewInBrowser:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            }

            return base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        protected override int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == VsMenus.guidVsUIHierarchyWindowCmds) {
                switch ((VSConstants.VsUIHierarchyWindowCmdIds)nCmdID) {
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick:
                        int res;
                        if (TryHandleRightClick(pvaIn, out res)) {
                            return res;
                        }
                        break;
                }
            } else if (pguidCmdGroup == GuidList.guidDjangoCmdSet) {
                switch (nCmdID) {
                    case PkgCmdIDList.cmdidValidateDjangoApp:
                        var pyProj = innerVsHierarchy.GetPythonInterpreterFactory();
                        if (pyProj != null) {
                            
                            
                            
                            

                            var path = pyProj.Configuration.InterpreterPath;
                            var psi = new ProcessStartInfo(path, "manage.py validate");
                            
                            object projectDir;
                            ErrorHandler.ThrowOnFailure(innerVsHierarchy.GetProperty(
                                (uint)VSConstants.VSITEMID.Root, 
                                (int)__VSHPROPID.VSHPROPID_ProjectDir, 
                                out projectDir)
                            );

                            object projectName;
                            ErrorHandler.ThrowOnFailure(innerVsHierarchy.GetProperty(
                                (uint)VSConstants.VSITEMID.Root,
                                (int)__VSHPROPID.VSHPROPID_ProjectName,
                                out projectName)
                            );

                            // TODO: Using the project name to find manage.py here isn't quite right.  Do we search
                            // for manage.py, do we tag it in some special way?
                            psi.WorkingDirectory = System.IO.Path.Combine(projectDir.ToString(), projectName.ToString());

                            psi.CreateNoWindow = true;
                            psi.UseShellExecute = false;
                            psi.RedirectStandardOutput = true;
                            psi.RedirectStandardError = true;
                            
                            var proc = Process.Start(psi);
                            var dialog = new WaitForValidationDialog(proc);

                            ShowValidationDialog(dialog, proc);
                        } else {
                            MessageBox.Show("Could not find Python interpreter for project.");
                        }
                        break;
                }
            }

            return base.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private static void ShowValidationDialog(WaitForValidationDialog dialog, Process proc) {
            var curScheduler = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
            var receiver = new OutputDataReceiver(curScheduler, dialog);
            proc.OutputDataReceived += receiver.OutputDataReceived;
            proc.ErrorDataReceived += receiver.OutputDataReceived;
            
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            // when the process exits allow the user to press ok, disable cancelling...
            ThreadPool.QueueUserWorkItem(x => {
                proc.WaitForExit();
                var task = System.Threading.Tasks.Task.Factory.StartNew(
                    () =>  dialog.EnableOk(),
                    default(CancellationToken),
                    System.Threading.Tasks.TaskCreationOptions.None,
                    curScheduler
                );
                task.Wait();
                if (task.Exception != null) {
                    Debug.Assert(false);
                    Debug.WriteLine(task.Exception);
                }
            });

            dialog.ShowDialog();
            dialog.SetText(receiver.Received.ToString());
        }

        class OutputDataReceiver {
            public readonly StringBuilder Received = new StringBuilder();
            private readonly TaskScheduler _scheduler;
            private readonly WaitForValidationDialog _dialog;

            public OutputDataReceiver(TaskScheduler scheduler, WaitForValidationDialog dialog) {
                _scheduler = scheduler;
                _dialog = dialog;
            }

            public void OutputDataReceived(object sender, DataReceivedEventArgs e) {
                Received.Append(e.Data);                
                System.Threading.Tasks.Task.Factory.StartNew(
                    () => _dialog.SetText(Received.ToString()),
                    default(CancellationToken),
                    System.Threading.Tasks.TaskCreationOptions.None,
                    _scheduler
                );
            }
        }

        private bool TryHandleRightClick(IntPtr pvaIn, out int res) {
            IVsMonitorSelection monitorSelection = _package.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainer = IntPtr.Zero;
            try {
                uint selectionItemId;
                IVsMultiItemSelect multiItemSelect = null;
                ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(out hierarchyPtr, out selectionItemId, out multiItemSelect, out selectionContainer));

                if (selectionItemId != VSConstants.VSITEMID_NIL && hierarchyPtr != IntPtr.Zero) {
                    IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;

                    if (selectionItemId != VSConstants.VSITEMID_SELECTION) {
                        // This is a single selection. Compare hirarchy with our hierarchy and get node from itemid
                        if (Utilities.IsSameComObject(this, hierarchy)) {
                            Guid propGuid;
                            ErrorHandler.ThrowOnFailure(hierarchy.GetGuidProperty(selectionItemId, (int)__VSHPROPID.VSHPROPID_TypeGuid, out propGuid));

                            if (TryShowContextMenu(pvaIn, propGuid, out res)) {
                                return true;
                            }
                        }
                    } else if (multiItemSelect != null) {
                        // This is a multiple item selection.
                        // Get number of items selected and also determine if the items are located in more than one hierarchy

                        uint numberOfSelectedItems;
                        int isSingleHierarchyInt;
                        ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt));
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        // Now loop all selected items and add to the list only those that are selected within this hierarchy
                        if (!isSingleHierarchy || (isSingleHierarchy && Utilities.IsSameComObject(this, hierarchy))) {
                            Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
                            VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            uint flags = (isSingleHierarchy) ? (uint)__VSGSIFLAGS.GSI_fOmitHierPtrs : 0;
                            ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(flags, numberOfSelectedItems, vsItemSelections));
                            Guid itemType = Guid.Empty;
                            foreach (VSITEMSELECTION vsItemSelection in vsItemSelections) {
                                Guid typeGuid;
                                ErrorHandler.ThrowOnFailure(vsItemSelection.pHier.GetGuidProperty(vsItemSelection.itemid, (int)__VSHPROPID.VSHPROPID_TypeGuid, out typeGuid));

                                if (itemType == Guid.Empty) {
                                    itemType = typeGuid;
                                } else if (itemType != typeGuid) {
                                    // we have multiple item types
                                    itemType = Guid.Empty;
                                    break;
                                }
                            }

                            if (TryShowContextMenu(pvaIn, itemType, out res)) {
                                return true;
                            }
                        }
                    }
                }
            } finally {
                if (hierarchyPtr != IntPtr.Zero) {
                    Marshal.Release(hierarchyPtr);
                }
                if (selectionContainer != IntPtr.Zero) {
                    Marshal.Release(selectionContainer);
                }
            }

            res = VSConstants.E_FAIL;
            return false;
        }

        private bool TryShowContextMenu(IntPtr pvaIn, Guid itemType, out int res) {
            if (itemType == PythonProjectGuid) {
                // multiple Python prjoect nodes selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_WEBPROJECT);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFile) {
                // multiple files selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_WEBITEMNODE);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFolder) {
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_WEBFOLDER);
                return true;
            }
            res = VSConstants.E_FAIL;
            return false;
        }

        private int ShowContextMenu(IntPtr pvaIn, int ctxMenu) {
            object variant = Marshal.GetObjectForNativeVariant(pvaIn);
            UInt32 pointsAsUint = (UInt32)variant;
            short x = (short)(pointsAsUint & 0x0000ffff);
            short y = (short)((pointsAsUint & 0xffff0000) / 0x10000);

            POINTS points = new POINTS();
            points.x = x;
            points.y = y;

            return ShowContextMenu(ctxMenu, VsMenus.guidSHLMainMenu, points);
        }


        /// <summary>
        /// Shows the specified context menu at a specified location.
        /// </summary>
        /// <param name="menuId">The context menu ID.</param>
        /// <param name="groupGuid">The GUID of the menu group.</param>
        /// <param name="points">The location at which to show the menu.</param>
        protected virtual int ShowContextMenu(int menuId, Guid menuGroup, POINTS points) {
            IVsUIShell shell = _package.GetService(typeof(SVsUIShell)) as IVsUIShell;

            Debug.Assert(shell != null, "Could not get the ui shell from the project");
            if (shell == null) {
                return VSConstants.E_FAIL;
            }
            POINTS[] pnts = new POINTS[1];
            pnts[0].x = points.x;
            pnts[0].y = points.y;
            return shell.ShowContextMenu(0, ref menuGroup, menuId, pnts, (Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)this);
        }


        /// <summary>
        /// This should first QI for (and keep a reference to) each interface we plan to call on the inner project
        /// and then call the base implementation to do the rest. Because the base implementation
        /// already keep a reference to the interfaces it override, we don't need to QI for those.
        /// </summary>
        protected override void SetInnerProject(object inner) {
            // The reason why we keep a reference to those is that doing a QI after being
            // aggregated would do the AddRef on the outer object.
            _innerVsProjectFlavorCfgProvider = inner as IVsProjectFlavorCfgProvider;

            // Ensure we have a service provider as this is required for menu items to work
            if (this.serviceProvider == null)
                this.serviceProvider = (System.IServiceProvider)this._package;

            // Now let the base implementation set the inner object
            base.SetInnerProject(inner);
            /*
            // Add our commands (this must run after we called base.SetInnerProject)
            MSVSIP.OleMenuCommandService mcs = ((System.IServiceProvider)this).GetService(typeof(IMenuCommandService)) as MSVSIP.OleMenuCommandService;
            if (mcs != null) {
                // Command to show the generated target file
                CommandID cmd = new CommandID(GuidList.guidProjectSubtypeCmdSet, PkgCmdIDList.cmdidShowTargetFile);
                MenuCommand menuCmd = new MenuCommand(new EventHandler(ShowTargetFile), cmd);
                menuCmd.Supported = true;
                menuCmd.Visible = true;
                menuCmd.Enabled = true;
                mcs.AddCommand(menuCmd);
            }*/
        }


        #endregion
    }
}

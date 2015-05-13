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
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using TestUtilities;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockTreeNode : ITreeNode, IFocusable, IOleCommandTarget {
        private readonly MockVs _mockVs;
        internal HierarchyItem _item;
        private string _editLabel;
        private int _selectionStart, _selectionLength;

        private const uint MK_CONTROL = 0x0008; //winuser.h
        private const uint MK_SHIFT = 0x0004;

        public MockTreeNode(MockVs mockVs, HierarchyItem res) {
            _mockVs = mockVs;
            _item = res;
        }

        public void Select() {
            _mockVs.SetFocus(this);
        }


        public void AddToSelection() {
            throw new NotImplementedException();
        }

        public void GetFocus() {
        }

        public void LostFocus() {
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                switch ((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Rename:
                        if ((_editLabel = _item.EditLabel) != null) {
                            _selectionLength = 0;
                            _selectionLength = _editLabel.Length - Path.GetExtension(_editLabel).Length;
                            return VSConstants.S_OK;
                        }
                        break;
                }
            } else if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        if (_editLabel != null) {
                            if (_selectionLength != 0) {
                                _editLabel = _editLabel.Remove(_selectionStart, _selectionLength);
                                _selectionLength = 0;
                            }
                            var ch = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                            _editLabel = _editLabel.Insert(_selectionStart, ch.ToString());
                            _selectionStart++;
                        }
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        if (_editLabel != null) {
                            _item.EditLabel = _editLabel;
                            _editLabel = null;
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.DELETE:
                        DeleteItem();
                        break;
                }
            }

            IOleCommandTarget target = _item.Hierarchy as IOleCommandTarget;
            if (target != null) {
                int hr = target.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                if (hr != (int)OleConstants.OLECMDERR_E_NOTSUPPORTED) {
                    return hr;
                }
            }

            IVsUIHierarchy uiHier = _item.Hierarchy as IVsUIHierarchy;
            if (uiHier != null) {
                return uiHier.ExecCommand(
                    _item.ItemId,
                    ref pguidCmdGroup,
                    nCmdID,
                    nCmdexecopt,
                    pvaIn,
                    pvaOut
                );
            }
            return VSConstants.E_FAIL;
        }

        private void DeleteItem() {
            var deleteHandler = _item.Hierarchy as IVsHierarchyDeleteHandler;
            int canRemoveItem = 0, canDeleteItem = 0;
            if (deleteHandler != null &&
                ErrorHandler.Succeeded(deleteHandler.QueryDeleteItem((uint)__VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject, _item.ItemId, out canRemoveItem))) {
                deleteHandler.QueryDeleteItem((uint)__VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage, _item.ItemId, out canDeleteItem);
            }
            bool showSpecificMsg = ShouldShowSpecificMessage(canRemoveItem, canDeleteItem);
            if (canRemoveItem != 0) {
                if (canDeleteItem != 0) {
                    // show delete or remove dialog...
                } else {
                    // show remove dialog...
                    PrmoptAndDelete(
                        deleteHandler,
                        __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject,
                        ""
                    );
                }

            } else if (canDeleteItem != 0) {
                object name;
                ErrorHandler.ThrowOnFailure(_item.Hierarchy.GetProperty(_item.ItemId, (int)__VSHPROPID.VSHPROPID_Name, out name));
                string message = string.Format("'{0}' will be deleted permanently.", name);
                PrmoptAndDelete(
                    deleteHandler, 
                    __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage, 
                    message
                );
            }
        }

        private void PrmoptAndDelete(IVsHierarchyDeleteHandler deleteHandler, __VSDELETEITEMOPERATION deleteType,string message) {
            Guid unused = Guid.Empty;
            int result;
            // show delete dialog...
            if (ErrorHandler.Succeeded(
                _mockVs.UIShell.ShowMessageBox(
                    0,
                    ref unused,
                    null,
                    message,
                    null,
                    0,
                    OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                    OLEMSGICON.OLEMSGICON_WARNING,
                    0,
                    out result
                )) && result == DialogResult.OK) {
                int hr = deleteHandler.DeleteItem(
                    (uint)deleteType,
                    _item.ItemId
                );

                if (ErrorHandler.Failed(hr) && hr != VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    _mockVs.UIShell.ReportErrorInfo(hr);
                }
            }
        }

        private bool ShouldShowSpecificMessage(int canRemoveItem, int canDeleteItem) {
            __VSDELETEITEMOPERATION op = 0;
            if (canRemoveItem != 0) {
                op |= __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
            }
            if (canDeleteItem != 0) {
                op |= __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage;
            }

            IVsHierarchyDeleteHandler2 deleteHandler = _item.Hierarchy as IVsHierarchyDeleteHandler2;
            if (deleteHandler != null) {
                int dwShowStandardMessage;
                uint pdwDelItemOp;
                deleteHandler.ShowSpecificDeleteRemoveMessage(
                    (uint)op, 
                    1, 
                    new[] { _item.ItemId }, 
                    out dwShowStandardMessage, 
                    out pdwDelItemOp
                );
                return dwShowStandardMessage != 0;
            }
            return false;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            IOleCommandTarget target = _item.Hierarchy as IOleCommandTarget;
            if (target != null) {
                int hr = target.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                if (hr != (int)OleConstants.OLECMDERR_E_NOTSUPPORTED) {
                    return hr;
                }
            }

            IVsUIHierarchy uiHier = _item.Hierarchy as IVsUIHierarchy;
            if (uiHier != null) {
                return uiHier.QueryStatusCommand(
                    _item.ItemId,
                    ref pguidCmdGroup,
                    cCmds,
                    prgCmds,
                    pCmdText                    
                );
            }

            return VSConstants.E_FAIL;
        }

        public void DragOntoThis(params ITreeNode[] source) {
            DragOntoThis(Key.None, source);
        }

        public void DragOntoThis(Key modifier, params ITreeNode[] source) {
            _mockVs.Invoke(() => DragOntoThisUIThread(modifier, source));
        }

        private void DragOntoThisUIThread(Key modifier, ITreeNode[] source) {
            var target = _item.Hierarchy as IVsHierarchyDropDataTarget;
            if (target != null) {
                uint effect = 0;
                uint keyState = GetKeyState(modifier);

                source[0].Select();
                for (int i = 1; i < source.Length; i++) {
                    source[i].AddToSelection();
                }

                MockTreeNode sourceNode = (MockTreeNode)source[0];
                var dropDataSource = (IVsHierarchyDropDataSource2)sourceNode._item.Hierarchy;
                uint okEffects;
                IDataObject data;
                IDropSource dropSource;
                ErrorHandler.ThrowOnFailure(dropDataSource.GetDropInfo(out okEffects, out data, out dropSource));

                int hr = hr = target.DragEnter(
                    data,
                    keyState,
                    _item.ItemId,
                    ref effect
                );

                if (ErrorHandler.Succeeded(hr)) {
                    if (effect == 0) {
                        return;
                    }

                    hr = target.DragOver(keyState, _item.ItemId, ref effect);

                    if (ErrorHandler.Succeeded(hr)) {
                        int cancel;
                        ErrorHandler.ThrowOnFailure(
                            dropDataSource.OnBeforeDropNotify(
                                data,
                                effect,
                                out cancel
                            )
                        );

                        if (cancel == 0) {
                            hr = target.Drop(
                                data,
                                keyState,
                                _item.ItemId,
                                ref effect
                            );
                        }

                        int dropped = 0;
                        if (cancel == 0 && ErrorHandler.Succeeded(hr)) {
                            dropped = 1;
                        }
                        ErrorHandler.ThrowOnFailure(dropDataSource.OnDropNotify(dropped, effect));
                    }
                }
                return;
            }
            throw new NotImplementedException();
        }

        private uint GetKeyState(Key modifier) {
            switch (modifier) {
                case Key.LeftShift:
                    return MK_SHIFT;
                case Key.LeftCtrl:
                    return MK_CONTROL;
                case Key.None:
                    return 0;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

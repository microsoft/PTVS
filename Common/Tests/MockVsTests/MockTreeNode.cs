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
                switch((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Rename:
                        if ((_editLabel = _item.EditLabel) != null) {
                            _selectionLength = 0;
                            _selectionLength = _editLabel.Length - Path.GetExtension(_editLabel).Length;
                            return VSConstants.S_OK;
                        }
                        break;
                }
            } else if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch((VSConstants.VSStd2KCmdID)nCmdID) {
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
            throw new NotImplementedException();
        }
    }
}

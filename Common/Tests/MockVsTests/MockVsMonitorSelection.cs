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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockVsMonitorSelection : IVsMonitorSelection {
        private readonly MockVs _vs;
        private uint _lastSelectionEventsCookie;
        private readonly Dictionary<uint, IVsSelectionEvents> _listeners = new Dictionary<uint, IVsSelectionEvents>();

        private uint _lastCmdUIContextCookie = 0;
        private readonly Dictionary<uint, Guid> _cmdUIContexts = new Dictionary<uint, Guid>();
        private readonly Dictionary<Guid, uint> _cmdUIContextsByGuid = new Dictionary<Guid, uint>();
        private readonly List<bool> _cmdUIContextsActive = new List<bool> { false };

        public MockVsMonitorSelection(MockVs vs) {
            _vs = vs;
        }

        public int AdviseSelectionEvents(
            IVsSelectionEvents pSink,
            [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")]out uint pdwCookie) {
            _lastSelectionEventsCookie++;
            pdwCookie = _lastSelectionEventsCookie;
            _listeners.Add(pdwCookie, pSink);
            return VSConstants.S_OK;
        }

        public int UnadviseSelectionEvents(
            [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")]uint dwCookie) {
            _listeners.Remove(dwCookie);
            return VSConstants.S_OK;
        }

        public int GetCmdUIContextCookie(
            [ComAliasName("Microsoft.VisualStudio.OLE.Interop.REFGUID")]ref Guid rguidCmdUI,
            [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")]out uint pdwCmdUICookie) {
            if (_cmdUIContextsByGuid.TryGetValue(rguidCmdUI, out pdwCmdUICookie)) {
                return VSConstants.S_OK;
            }

            _lastCmdUIContextCookie++;
            pdwCmdUICookie = _lastCmdUIContextCookie;
            _cmdUIContexts.Add(pdwCmdUICookie, rguidCmdUI);
            _cmdUIContextsByGuid.Add(rguidCmdUI, pdwCmdUICookie);
            _cmdUIContextsActive.Add(false);
            return VSConstants.S_OK;
        }

        public int IsCmdUIContextActive(
            [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")]uint dwCmdUICookie,
            [ComAliasName("Microsoft.VisualStudio.OLE.Interop.BOOL")]out int pfActive) {
            pfActive = _cmdUIContextsActive[(int)dwCmdUICookie] ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int SetCmdUIContext(
            [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")]uint dwCmdUICookie,
            [ComAliasName("Microsoft.VisualStudio.OLE.Interop.BOOL")]int fActive) {
            _cmdUIContextsActive[(int)dwCmdUICookie] = fActive != 0;
            foreach (var kvp in _listeners) {
                kvp.Value.OnCmdUIContextChanged(dwCmdUICookie, fActive);
            }

            return VSConstants.S_OK;
        }


        public int GetCurrentSelection(out IntPtr ppHier, out uint pitemid, out IVsMultiItemSelect ppMIS, out IntPtr ppSC) {
            ppMIS = null;
            ppSC = IntPtr.Zero;

            MockTreeNode node = _vs._focused as MockTreeNode;
            if (node == null) {
                ppHier = IntPtr.Zero;
                pitemid = (uint)VSConstants.VSITEMID.Nil;
                return VSConstants.S_OK;
            }
            ppHier = Marshal.GetIUnknownForObject(node._item.Hierarchy);
            pitemid = node._item.ItemId;
            return VSConstants.S_OK;
        }

        public int GetCurrentElementValue([ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSSELELEMID")]uint elementid, out object pvarValue) {
            throw new NotImplementedException();
        }
    }
}

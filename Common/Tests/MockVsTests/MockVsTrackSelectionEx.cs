// Visual Studio Shared Project
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockVsTrackSelectionEx : IVsTrackSelectionEx
    {
        private readonly MockVsMonitorSelection _monSel;
        private IVsHierarchy _curHierarchy;
        private uint _itemid;
        private IVsMultiItemSelect _multiSel;
        private ISelectionContainer _selectionContainer;
        private static IntPtr HIERARCHY_DONTCHANGE = new IntPtr(-1);

        public MockVsTrackSelectionEx(MockVsMonitorSelection monSel)
        {
            _monSel = monSel;
        }

        public int GetCurrentSelection(out IntPtr ppHier, out uint pitemid, out IVsMultiItemSelect ppMIS, out IntPtr ppSC)
        {
            if (_curHierarchy != null)
            {
                ppHier = Marshal.GetIUnknownForObject(_curHierarchy);
            }
            else
            {
                ppHier = IntPtr.Zero;
            }
            pitemid = _itemid;
            ppMIS = _multiSel;
            if (_selectionContainer != null)
            {
                ppSC = Marshal.GetIUnknownForObject(_selectionContainer);
            }
            else
            {
                ppSC = IntPtr.Zero;
            }
            return VSConstants.S_OK;
        }

        public void GetCurrentSelection(out IVsHierarchy ppHier, out uint pitemid, out IVsMultiItemSelect ppMIS, out ISelectionContainer ppSC)
        {
            ppHier = _curHierarchy;
            pitemid = _itemid;
            ppMIS = _multiSel;
            ppSC = _selectionContainer;
        }

        public int IsMyHierarchyCurrent(out int pfCurrent)
        {
            throw new NotImplementedException();
        }

        public int OnElementValueChange(uint elementid, int fDontPropagate, object varValue)
        {
            _monSel.NotifyElementChanged(this, elementid);
            return VSConstants.S_OK;
        }

        public int OnSelectChange(ISelectionContainer pSC)
        {
            var sc = Marshal.GetIUnknownForObject(pSC);
            try
            {
                return OnSelectChangeEx(
                    HIERARCHY_DONTCHANGE,
                    VSConstants.VSITEMID_NIL,
                    null,
                    sc
                );
            }
            finally
            {
                Marshal.Release(sc);
            }
        }

        public int OnSelectChangeEx(IntPtr pHier, uint itemid, IVsMultiItemSelect pMIS, IntPtr pSC)
        {
            if (pHier != HIERARCHY_DONTCHANGE)
            {
                if (pHier != IntPtr.Zero)
                {
                    _curHierarchy = Marshal.GetObjectForIUnknown(pHier) as IVsHierarchy;
                }
                else
                {
                    _curHierarchy = null;
                }
                _itemid = itemid;
            }
            _multiSel = pMIS;
            if (pSC != null)
            {
                _selectionContainer = (ISelectionContainer)Marshal.GetObjectForIUnknown(pSC);
            }
            else
            {
                _selectionContainer = null;
            }

            _monSel.NotifySelectionContextChanged(this);

            return VSConstants.S_OK;
        }

        public void OnSelectChangeEx(IVsHierarchy pHier, uint itemid, IVsMultiItemSelect pMIS, ISelectionContainer pSC)
        {
            _curHierarchy = pHier;
            _itemid = itemid;
            OnSelectChangeEx(pMIS, pSC);
        }

        public void OnSelectChangeEx(IVsMultiItemSelect pMIS, ISelectionContainer pSC)
        {
            _multiSel = pMIS;
            _selectionContainer = pSC;

            _monSel.NotifySelectionContextChanged(this);
        }
    }
}

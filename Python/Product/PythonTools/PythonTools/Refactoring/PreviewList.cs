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
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Implements the VS interfaces for a IVsPreviewChangesList and IVsLiteTreeList which are used to display
    /// our preview.  Most of the core logic for the individual items is implemnted via the IPreviewItem interface.
    /// </summary>
    class PreviewList : IVsLiteTreeList, IVsPreviewChangesList {
        private readonly IPreviewItem[] _items;

        public PreviewList(IPreviewItem[] items) {
            _items = items;
        }

        public IPreviewItem[] Items {
            get {
                return _items;
            }
        }

        public int GetDisplayData(uint index, VSTREEDISPLAYDATA[] pData) {
            var item = _items[index];
            pData[0].SelectedImage = pData[0].Image = item.Glyph;
            pData[0].hImageList = item.ImageList;
            pData[0].State = (uint)item.CheckState << 12;
            var selectedSpan = item.Selection;
            if (selectedSpan != null) {
                pData[0].State |= (uint)_VSTREEDISPLAYSTATE.TDS_FORCESELECT;
                pData[0].ForceSelectStart = (ushort)selectedSpan.Value.Start;
                pData[0].ForceSelectLength = (ushort)selectedSpan.Value.Length;
            }
            return VSConstants.S_OK;
        }

        public int GetExpandable(uint index, out int pfExpandable) {
            var item = _items[index];
            pfExpandable = item.IsExpandable ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int GetExpandedList(uint index, out int pfCanRecurse, out IVsLiteTreeList pptlNode) {
            var item = _items[index];
            pptlNode = item.Children;
            pfCanRecurse = item.IsExpandable ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int GetFlags(out uint pFlags) {
            pFlags = 0;
            return VSConstants.S_OK;
        }

        public int GetItemCount(out uint pCount) {
            pCount = (uint)_items.Length;
            return VSConstants.S_OK;
        }

        public int GetListChanges(ref uint pcChanges, VSTREELISTITEMCHANGE[] prgListChanges) {
            if (prgListChanges == null) {
                pcChanges = (uint)_items.Length;
                return VSConstants.S_OK;
            }
            for (int i = 0; i < pcChanges; i++) {
                prgListChanges[i].grfChange = (uint)_VSTREEITEMCHANGESMASK.TCT_ITEMNAMECHANGED;
                prgListChanges[i].index = (uint)i;
            }
            return VSConstants.S_OK;
        }

        public int GetText(uint index, VSTREETEXTOPTIONS tto, out string ppszText) {
            ppszText = _items[index].GetText(tto);

            return VSConstants.S_OK;
        }

        public int GetTipText(uint index, VSTREETOOLTIPTYPE eTipType, out string ppszText) {
            ppszText = null;
            return VSConstants.E_NOTIMPL;
        }

        public int LocateExpandedList(IVsLiteTreeList ExpandedList, out uint iIndex) {
            throw new NotImplementedException();
        }

        public int OnClose(VSTREECLOSEACTIONS[] ptca) {
            foreach (var item in _items) {
                item.Close(ptca[0]);
            }
            return VSConstants.S_OK;
        }

        public int OnRequestSource(uint index, object pIUnknownTextView) {
            if (pIUnknownTextView == null) {
                return VSConstants.E_POINTER;
            }

            IVsTextView view = pIUnknownTextView as IVsTextView;
            if (view == null) {
                return VSConstants.E_NOINTERFACE;
            }

            _items[index].DisplayPreview(view);

            return VSConstants.S_OK;
        }

        public int ToggleState(uint index, out uint ptscr) {
            var item = _items[index];
            ptscr = (uint)item.ToggleState();
            return VSConstants.S_OK;
        }

        public int UpdateCounter(out uint pCurUpdate, out uint pgrfChanges) {
            pCurUpdate = 0;
            pgrfChanges = 0;
            return VSConstants.E_NOTIMPL;
        }
    }
}

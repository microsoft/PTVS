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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockVsUIHierarchyWindow : IVsUIHierarchyWindow {
        public int AddUIHierarchy(IVsUIHierarchy pUIH, uint grfAddOptions) {
            throw new NotImplementedException();
        }

        public int ExpandItem(IVsUIHierarchy pUIH, uint itemid, EXPANDFLAGS expf) {
            throw new NotImplementedException();
        }

        public int FindCommonSelectedHierarchy(uint grfOpt, out IVsUIHierarchy lppCommonUIH) {
            throw new NotImplementedException();
        }

        public int GetCurrentSelection(out IntPtr ppHier, out uint pitemid, out IVsMultiItemSelect ppMIS) {
            throw new NotImplementedException();
        }

        public int GetItemState(IVsUIHierarchy pHier, uint itemid, uint dwStateMask, out uint pdwState) {
            throw new NotImplementedException();
        }

        public int Init(IVsUIHierarchy pUIH, uint grfUIHWF, out object ppunkOut) {
            throw new NotImplementedException();
        }

        public int RemoveUIHierarchy(IVsUIHierarchy pUIH) {
            throw new NotImplementedException();
        }

        public int SetCursor(IntPtr hNewCursor, out IntPtr phOldCursor) {
            throw new NotImplementedException();
        }

        public int SetWindowHelpTopic(string lpszHelpFile, uint dwContext) {
            throw new NotImplementedException();
        }
    }
}

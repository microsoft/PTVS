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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.VisualStudioTools {

    class ClipboardService : IClipboardService {
        public void SetClipboard(IDataObject dataObject) {
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleSetClipboard(dataObject));
        }

        public IDataObject GetClipboard() {
            IDataObject res;
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleGetClipboard(out res));
            return res;
        }

        public void FlushClipboard() {
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleFlushClipboard());
        }

        public bool OpenClipboard() {
            int res = UnsafeNativeMethods.OpenClipboard(IntPtr.Zero);
            ErrorHandler.ThrowOnFailure(res);
            return res == 1;
        }

        public void EmptyClipboard() {
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.EmptyClipboard());
        }

        public void CloseClipboard() {
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.CloseClipboard());
        }
    }
}
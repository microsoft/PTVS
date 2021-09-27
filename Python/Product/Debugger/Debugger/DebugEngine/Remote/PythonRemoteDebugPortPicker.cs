// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Debugger.Remote
{
    // This class is used by the Attach to Process dialog when Python remote debug transport is active
    // to implement the "Find" button.
    [ComVisible(true)]
    [Guid("FB6A6E8D-47C2-4D0E-BB44-609887EF2327")]
    public class PythonRemoteDebugPortPicker : IDebugPortPicker
    {
        public int DisplayPortPicker(IntPtr hwndParentDialog, out string pbstrPortId)
        {
            pbstrPortId = null;
            return VSConstants.E_NOTIMPL;
        }

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider pSP)
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}

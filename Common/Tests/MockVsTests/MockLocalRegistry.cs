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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockLocalRegistry : ILocalRegistry, ILocalRegistryCorrected
    {
        private static Guid AggregatorGuid = new Guid("{C402364C-5474-47e7-AE72-BF5418780221}");

        public int CreateInstance(Guid clsid, object punkOuter, ref Guid riid, uint dwFlags, out IntPtr ppvObj)
        {
            throw new NotImplementedException();
        }

        public int GetClassObjectOfClsid(ref Guid clsid, uint dwFlags, IntPtr lpReserved, ref Guid riid, out IntPtr ppvClassObject)
        {
            throw new NotImplementedException();
        }

        public int GetTypeLibOfClsid(Guid clsid, out VisualStudio.OLE.Interop.ITypeLib pptLib)
        {
            throw new NotImplementedException();
        }

        public int CreateInstance(Guid clsid, IntPtr punkOuterIUnknown, ref Guid riid, uint dwFlags, out IntPtr ppvObj)
        {
            if (clsid == typeof(Microsoft.VisualStudio.ProjectAggregator.CProjectAggregatorClass).GUID)
            {
                var res = new ProjectAggregator();
                ppvObj = Marshal.GetIUnknownForObject(res);
                return VSConstants.S_OK;
            }
            throw new NotImplementedException();
        }
    }

}

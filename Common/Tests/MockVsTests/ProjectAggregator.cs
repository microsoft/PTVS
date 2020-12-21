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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudioTools.MockVsTests
{

    class ProjectAggregator : IVsProjectAggregator2, ICustomQueryInterface
    {
        private IntPtr _inner;
        private IntPtr _project;

        public int SetInner(IntPtr innerIUnknown)
        {
            _inner = innerIUnknown;
            return VSConstants.S_OK;
        }

        public int SetMyProject(IntPtr projectIUnknown)
        {
            _project = projectIUnknown;
            return VSConstants.S_OK;
        }

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            if (_project != IntPtr.Zero)
            {
                if (ErrorHandler.Succeeded(Marshal.QueryInterface(_project, ref iid, out ppv)))
                {
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            if (_inner != IntPtr.Zero)
            {
                if (ErrorHandler.Succeeded(Marshal.QueryInterface(_inner, ref iid, out ppv)))
                {
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            ppv = IntPtr.Zero;
            return CustomQueryInterfaceResult.Failed;
        }
    }
}

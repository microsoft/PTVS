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
using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockVsShell : IVsShell
    {
        private readonly Dictionary<int, object> _properties = new Dictionary<int, object>();

        public int AdviseBroadcastMessages(IVsBroadcastMessageEvents pSink, out uint pdwCookie)
        {
            throw new NotImplementedException();
        }

        public int AdviseShellPropertyChanges(IVsShellPropertyEvents pSink, out uint pdwCookie)
        {
            throw new NotImplementedException();
        }

        public int GetPackageEnum(out IEnumPackages ppenum)
        {
            throw new NotImplementedException();
        }

        public int GetProperty(int propid, out object pvar)
        {
            return _properties.TryGetValue(propid, out pvar) ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public int IsPackageInstalled(ref Guid guidPackage, out int pfInstalled)
        {
            throw new NotImplementedException();
        }

        public int IsPackageLoaded(ref Guid guidPackage, out IVsPackage ppPackage)
        {
            throw new NotImplementedException();
        }

        public int LoadPackage(ref Guid guidPackage, out IVsPackage ppPackage)
        {
            throw new NotImplementedException();
        }

        public int LoadPackageString(ref Guid guidPackage, uint resid, out string pbstrOut)
        {
            throw new NotImplementedException();
        }

        public int LoadUILibrary(ref Guid guidPackage, uint dwExFlags, out uint phinstOut)
        {
            throw new NotImplementedException();
        }

        public int SetProperty(int propid, object var)
        {
            _properties[propid] = var;
            return VSConstants.S_OK;
        }

        public int UnadviseBroadcastMessages(uint dwCookie)
        {
            throw new NotImplementedException();
        }

        public int UnadviseShellPropertyChanges(uint dwCookie)
        {
            throw new NotImplementedException();
        }
    }
}

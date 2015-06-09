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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockVsShell : IVsShell {
        private readonly Dictionary<int, object> _properties = new Dictionary<int, object>();

        public int AdviseBroadcastMessages(IVsBroadcastMessageEvents pSink, out uint pdwCookie) {
            throw new NotImplementedException();
        }

        public int AdviseShellPropertyChanges(IVsShellPropertyEvents pSink, out uint pdwCookie) {
            throw new NotImplementedException();
        }

        public int GetPackageEnum(out IEnumPackages ppenum) {
            throw new NotImplementedException();
        }

        public int GetProperty(int propid, out object pvar) {
            return _properties.TryGetValue(propid, out pvar) ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public int IsPackageInstalled(ref Guid guidPackage, out int pfInstalled) {
            throw new NotImplementedException();
        }

        public int IsPackageLoaded(ref Guid guidPackage, out IVsPackage ppPackage) {
            throw new NotImplementedException();
        }

        public int LoadPackage(ref Guid guidPackage, out IVsPackage ppPackage) {
            throw new NotImplementedException();
        }

        public int LoadPackageString(ref Guid guidPackage, uint resid, out string pbstrOut) {
            throw new NotImplementedException();
        }

        public int LoadUILibrary(ref Guid guidPackage, uint dwExFlags, out uint phinstOut) {
            throw new NotImplementedException();
        }

        public int SetProperty(int propid, object var) {
            _properties[propid] = var;
            return VSConstants.S_OK;
        }

        public int UnadviseBroadcastMessages(uint dwCookie) {
            throw new NotImplementedException();
        }

        public int UnadviseShellPropertyChanges(uint dwCookie) {
            throw new NotImplementedException();
        }
    }
}

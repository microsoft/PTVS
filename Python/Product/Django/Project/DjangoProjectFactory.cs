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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Django.Project {
    [Guid(DjangoProjectGuid)]
    public class DjangoProjectFactory : FlavoredProjectFactoryBase {
        internal const string DjangoProjectGuid = "5F0BE9CA-D677-4A4D-8806-6076C0FAAD37";
        private DjangoPackage _package;

        public DjangoProjectFactory(DjangoPackage package) {
            _package = package;
        }

        protected override object PreCreateForOuter(IntPtr outerProjectIUnknown) {
            var res = new DjangoProject();
            res._package = _package;
            return res;
        }
    }
}

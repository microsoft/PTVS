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

namespace Microsoft.VisualStudioTools.VSTestHost.Internal {
    static class Guids {
        public const string VSTestHostPkgString = "4a7e4285-12f6-4ceb-b878-f029a43a5315";
        public const string UnitTestTypeString = "13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b";

        public static readonly Guid VSTestHostPkg = new Guid(VSTestHostPkgString);
    };
}
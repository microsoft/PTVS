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

namespace Microsoft.PythonTools.Django.Project {

    [ComVisible(true)]
    [Guid("A666B929-44D0-4D68-A62A-7440A2E96D44")]
    public sealed class ProjectSmuggler {
        internal readonly DjangoProject Project;

        internal ProjectSmuggler(DjangoProject project) {
            Project = project;
        }
    }
}

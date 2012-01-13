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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Specifies the kind of reference.  Currently we support references to .NET
    /// assemblies for IronPython and .pyds for C Python.
    /// </summary>
    public enum ProjectReferenceKind {
        None,
        /// <summary>
        /// The reference is to a .NET assembly.  The name is a fully qualified path to
        /// the assembly.
        /// </summary>
        Assembly,
        /// <summary>
        /// The reference is to a Python extension module.  The name is a fully qualified
        /// path to the .pyd file.
        /// </summary>
        ExtensionModule
    }
}

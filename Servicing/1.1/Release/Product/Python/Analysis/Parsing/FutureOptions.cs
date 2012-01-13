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

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Options which have been enabled using from __future__ import 
    /// </summary>
    [Flags]
    public enum FutureOptions {
        None = 0,
        /// <summary>
        /// Enable true division (1/2 == .5)
        /// </summary>
        TrueDivision = 0x0001,
        /// <summary>
        /// Enable usage of the with statement
        /// </summary>
        WithStatement = 0x0010,
        /// <summary>
        /// Enable absolute imports
        /// </summary>
        AbsoluteImports = 0x0020,
        /// <summary>
        /// Enable usage of print as a function for better compatibility with Python 3.0.
        /// </summary>
        PrintFunction = 0x0400,
        /// <summary>
        /// String Literals should be parsed as Unicode strings
        /// </summary>
        UnicodeLiterals = 0x2000,
    }

}

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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
        /// <summary>
        /// Annotations should be stored as strings and not evaluated
        /// </summary>
        Annotations = 0x4000,
    }

}

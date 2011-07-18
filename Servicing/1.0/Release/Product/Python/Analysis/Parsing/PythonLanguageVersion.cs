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

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Specifies the version of the Python language to be used for parsing.
    /// 
    /// Referred to from C++ in PyDebugAttach.cpp and must be kept in sync
    /// </summary>
    public enum PythonLanguageVersion {
        None = 0,
        V24 = 0x0204,
        V25 = 0x0205,
        V26 = 0x0206,
        V27 = 0x0207,
        V30 = 0x0300,
        V31 = 0x0301,
        V32 = 0x0302
    }

    public static class PythonLanguageVersionExtensions {
        public static bool Is2x(this PythonLanguageVersion version) {
            return (((int)version >> 8) & 0xff) == 2;
        }

        public static bool Is3x(this PythonLanguageVersion version) {
            return (((int)version >> 8) & 0xff) == 3;
        }
    }
}

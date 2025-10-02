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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing {
    /// <summary>
    /// Specifies the version of the Python language to be used for parsing.
    /// 
    /// Referred to from C++ in PyDebugAttach.cpp and python.h and must be kept in sync
    /// </summary>
    public enum PythonLanguageVersion {
        None = 0,
        V24 = 0x0204,
        V25 = 0x0205,
        V26 = 0x0206,
        V27 = 0x0207,
        V30 = 0x0300,
        V31 = 0x0301,
        V32 = 0x0302,
        V33 = 0x0303,
        V34 = 0x0304,
        V35 = 0x0305,
        V36 = 0x0306,
        V37 = 0x0307,
        V38 = 0x0308,
        V39 = 0x0309,
        V310 = 0x030a,
        V311 = 0x030b,
        V312 = 0x030c,
        V313 = 0x030d,
        V314 = 0x030e, // Added for Python 3.14 support
    }

    public static class PythonLanguageVersionExtensions {
        public static bool Is2x(this PythonLanguageVersion version) => (((int)version >> 8) & 0xff) == 2;
        public static bool Is3x(this PythonLanguageVersion version) => (((int)version >> 8) & 0xff) == 3;
        public static bool IsNone(this PythonLanguageVersion version) => version == PythonLanguageVersion.None;

        public static bool IsImplicitNamespacePackagesSupported(this PythonLanguageVersion version) 
            => version >= PythonLanguageVersion.V33;

        public static Version ToVersion(this PythonLanguageVersion version) => new Version(((int)version) >> 8, ((int)version) & 0xff);

        public static PythonLanguageVersion ToLanguageVersion(this Version version) {
            var value = (version.Major << 8) + version.Minor;
            if (Enum.IsDefined(typeof(PythonLanguageVersion), value)) {
                return (PythonLanguageVersion)value;
            }
            else {
                Trace.WriteLine(Strings.PythonVersionNotSupportedTraceText.FormatUI(version));
                return PythonLanguageVersion.None;
            }
        }
    }
}

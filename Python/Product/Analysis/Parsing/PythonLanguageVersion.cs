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
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing {
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
        V37 = 0x0307
    }

    public static class PythonLanguageVersionExtensions {
        public static bool Is2x(this PythonLanguageVersion version) {
            return (((int)version >> 8) & 0xff) == 2;
        }

        public static bool Is3x(this PythonLanguageVersion version) {
            return (((int)version >> 8) & 0xff) == 3;
        }

        public static bool IsNone(this PythonLanguageVersion version) {
            return version == PythonLanguageVersion.None;
        }

        public static Version ToVersion(this PythonLanguageVersion version) {
            return new Version(((int)version) >> 8, ((int)version) & 0xff);
        }

        public static PythonLanguageVersion ToLanguageVersion(this Version version) {
            int value = (version.Major << 8) + version.Minor;
            if (Enum.IsDefined(typeof(PythonLanguageVersion), value)) {
                return (PythonLanguageVersion)value;
            }
            throw new InvalidOperationException("Unsupported Python version: {0}".FormatInvariant(version));
        }

    }
}

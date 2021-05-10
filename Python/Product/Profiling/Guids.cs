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

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.PythonTools.Profiling {
    static class GuidList {
        public const string guidPythonProfilingPkgString = "81DA0100-E6DB-4783-91EA-C38C3FA1B81E";
        public const string guidPythonProfilingCmdSetString = "C6E7BAFE-D4B6-4E04-85AF-9C83F18D8C78";
        public const string guidEditorFactoryString = "3585DC22-81A0-409E-85AE-CAE5D02D99CD";

        public static readonly Guid guidPythonProfilingCmdSet = new Guid(guidPythonProfilingCmdSetString);

        public static readonly Guid VsUIHierarchyWindow_guid = new Guid("{7D960B07-7AF8-11D0-8E5E-00A0C911005A}");
        public static readonly Guid guidEditorFactory = new Guid(guidEditorFactoryString);

        public static readonly Guid GuidPerfPkg = new Guid("{F4A63B2A-49AB-4b2d-AA59-A10F01026C89}");
    };
}
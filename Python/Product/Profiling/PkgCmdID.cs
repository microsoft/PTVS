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

// PkgCmdID.cs
// MUST match PkgCmdID.h

namespace Microsoft.PythonTools.Profiling {
    static class PkgCmdIDList {
        public const uint cmdidStartPythonProfiling = 0x100;
        public const uint cmdidPerfExplorer = 0x101;
        public const uint cmdidAddPerfSession = 0x102;
        public const uint cmdidStartProfiling = 0x103;

        public const uint cmdidPerfCtxStartProfiling = 0x104;
        public const uint cmdidPerfCtxSetAsCurrent = 0x105;
        public const uint cmdidReportsCompareReports = 0x106;
        public const uint cmdidReportsAddReport = 0x107;
        public const uint cmdidOpenReport = 0x108;
        public const uint cmdidStopProfiling = 0x109;

        public const uint menuIdPerfToolbar = 0x2000;
        public const uint menuIdPerfContext = 0x2001;
        public const uint menuIdPerfReportsContext = 0x2002;
        public const uint menuIdPerfSingleReportContext = 0x2003;

    };
}
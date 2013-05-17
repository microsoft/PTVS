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
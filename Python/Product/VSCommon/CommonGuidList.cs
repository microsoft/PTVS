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

namespace Microsoft.PythonTools.Common {
    // MUST match guids.h
    public static class CommonGuidList {
        public const string guidPythonToolsPkgString = "FC6A1BC9-C491-41DB-8D23-D33ABCCF84AE";
        public static readonly Guid guidPythonToolsPackage = new Guid(guidPythonToolsPkgString);

        public static readonly Guid guidCookiecutterPackage = new Guid("09B8B622-5967-4D96-B142-C1F89EA2E738");
        public static readonly Guid guidCookiecutterCmdSet = new Guid("A31E381E-CF66-4BED-B8FB-53B7C67FBFDA");

        // PTVS 2019
        public const string guidPythonToolsVS2019 = "6DBD7C1E-1F1B-496D-AC7C-C55DAE66C783";

        // Well known guids
        public const string guidMiscFilesProjectGuidString = "A2FE74E1-B743-11D0-AE1A-00A0C90FFFC3";

        public static readonly Guid guidCSharpProjectPacakge = new Guid("FAE04EC1-301F-11D3-BF4B-00C04F79EFBC");
        public static readonly Guid guidVenusCmdId = new Guid("C7547851-4E3A-4E5B-9173-FA6E9C8BD82C");
        public static readonly Guid guidWebPackgeCmdId = new Guid("822E3603-E573-47D2-ACF0-520E4CE641C2");
        public static readonly Guid guidWebPackageGuid = new Guid("D9A342D1-A429-4059-808A-E55EE6351F7F");
        public static readonly Guid guidWebAppCmdId = new Guid("CB26E292-901A-419c-B79D-49BD45C43929");
        public static readonly Guid guidEureka = new Guid("30947EBE-9147-45F9-96CF-401BFC671A82");  //  Microsoft.VisualStudio.Web.Eureka.dll package, includes page inspector
        public static readonly Guid guidOfficeSharePointCmdSet = new Guid("D26C976C-8EE8-4EC4-8746-F5F7702A17C5");
        // End of well known guids
    }
}
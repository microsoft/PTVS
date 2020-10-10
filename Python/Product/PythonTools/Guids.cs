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

namespace Microsoft.PythonTools {
    static class GuidList {
        // Well known guids
        public const string guidPythonToolsVS2019 = "6DBD7C1E-1F1B-496D-AC7C-C55DAE66C783";
        public const string guidMiscFilesProjectGuidString = "A2FE74E1-B743-11D0-AE1A-00A0C90FFFC3";

        public static readonly Guid guidCSharpProjectPacakge = new Guid("FAE04EC1-301F-11D3-BF4B-00C04F79EFBC");
        public static readonly Guid guidVenusCmdId = new Guid("c7547851-4e3a-4e5b-9173-fa6e9c8bd82c");
        public static readonly Guid guidWebPackgeCmdId = new Guid("822e3603-e573-47d2-acf0-520e4ce641c2");
        public static readonly Guid guidWebPackageGuid = new Guid("d9a342d1-a429-4059-808a-e55ee6351f7f");
        public static readonly Guid guidWebAppCmdId = new Guid("CB26E292-901A-419c-B79D-49BD45C43929");
        public static readonly Guid guidEureka = new Guid("30947ebe-9147-45f9-96cf-401bfc671a82");  //  Microsoft.VisualStudio.Web.Eureka.dll package, includes page inspector
        public static readonly Guid guidOfficeSharePointCmdSet = new Guid("d26c976c-8ee8-4ec4-8746-f5f7702a17c5");
        // End of well known guids

        public const string guidPythonToolsPkgString = "FC6A1BC9-C491-41DB-8D23-D33ABCCF84AE";
        public const string guidPythonToolsCmdSetString = "44AFF35A-AA6E-46AA-BC75-D4AC9F7E7655";
        public const string guidPythonProjectString = "A7656004-6CDD-4FEE-8132-483C642CA453";
        public const string guidPythonLanguageService = "B79D45B5-BED9-40AC-A365-DA6008CA27AA";
        public const string guidLoadedProjectInterpreterFactoryProviderString = "B8989820-F55D-4272-92F9-AA8D5954BA0A";
        public const string guidPythonInteractiveWindow = "99C69BDA-EDD8-4732-B827-957A97E92142";

        public static readonly Guid guidPythonToolsPackage = new Guid(guidPythonToolsPkgString);
        public static readonly Guid guidPythonToolsCmdSet = new Guid(guidPythonToolsCmdSetString);
        public static readonly Guid guidPythonProjectGuid = new Guid(guidPythonProjectString);
        public static readonly Guid guidPythonLanguageServiceGuid = new Guid(guidPythonLanguageService);
        public static readonly Guid guidPythonInteractiveWindowGuid = new Guid(guidPythonInteractiveWindow);
        public static readonly Guid guidPythonToolbarUIContext = new Guid("D03DDFA9-C339-468E-A0DC-CF917371ED57");
        public static readonly Guid guidCookiecutterPackage = new Guid("09B8B622-5967-4D96-B142-C1F89EA2E738");
        public static readonly Guid guidCookiecutterCmdSet = new Guid("A31E381E-CF66-4BED-B8FB-53B7C67FBFDA");
    };
}

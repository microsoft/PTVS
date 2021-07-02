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

namespace Microsoft.PythonTools.Django
{
    static class GuidList
    {
        public const string guidDjangoPkgString = "a8637c34-aa55-46e2-973c-9c3e09afc17b";
        public const string guidDjangoCmdSetString = "5b3281a5-d037-4e84-93aa-a6819304dbd9";
        public const string guidDjangoKeyBindingString = "96108b8f-2a98-4f6b-a6b6-69e04e7b7d3f";
        public const string guidDjangoEditorFactoryString = "E1B7ABDE-CDDE-4874-A8A6-5B5C7597A848";
        public const string guidDjangoPropertyPageString = "cf789a3e-b237-46df-a120-facc3581550f";

        public static readonly Guid guidDjangoCmdSet = new Guid(guidDjangoCmdSetString);
        public static readonly Guid guidDjangoEditorFactory = new Guid(guidDjangoEditorFactoryString);
        public static readonly Guid guidDjangoPropertyPage = new Guid(guidDjangoPropertyPageString);
        public static readonly Guid guidVenusCmdId = new Guid("c7547851-4e3a-4e5b-9173-fa6e9c8bd82c");
        public static readonly Guid guidWebPackgeCmdId = new Guid("822e3603-e573-47d2-acf0-520e4ce641c2");
        public static readonly Guid guidWebPackageGuid = new Guid("d9a342d1-a429-4059-808a-e55ee6351f7f");
        public static readonly Guid guidWebAppCmdId = new Guid("CB26E292-901A-419c-B79D-49BD45C43929");
        public static readonly Guid guidEureka = new Guid("30947ebe-9147-45f9-96cf-401bfc671a82");  //  Microsoft.VisualStudio.Web.Eureka.dll package, includes page inspector

        public static readonly Guid guidOfficeSharePointCmdSet = new Guid("d26c976c-8ee8-4ec4-8746-f5f7702a17c5");
    }
}
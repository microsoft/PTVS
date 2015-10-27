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

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.PythonTools.ML
{
    static class GuidList
    {
        public const string guidPythonMLPkgString = "185d223a-f461-4197-bd14-de0236e7eeca";
        public const string guidPythonMLCmdSetString = "D94D6774-B2DB-4291-9F27-D497EA92DA52";

        public static readonly Guid guidPythonMLCmdSet = new Guid(guidPythonMLCmdSetString);

        public static readonly Guid FlaskGuid = new Guid("{789894C7-04A9-4A11-A6B5-3F4435165112}"); // Flask Web Project marker
        public static readonly Guid BottleGuid = new Guid("{E614C764-6D9E-4607-9337-B7073809A0BD}"); // Bottle Web Project marker
        public static readonly Guid WorkerRoleGuid = new Guid("{725071E1-96AE-4405-9303-1BA64EFF6EBD}");  // Worker Role Project marker
        public static readonly Guid DjangoGuid = new Guid("{5F0BE9CA-D677-4A4D-8806-6076C0FAAD37}"); // Django


    }
}
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

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.PythonTools.Django {
    static class GuidList {
        public const string guidDjangoPkgString = "a8637c34-aa55-46e2-973c-9c3e09afc17b";
        public const string guidDjangoCmdSetString = "5b3281a5-d037-4e84-93aa-a6819304dbd9";
        public const string guidDjangoKeyBindingString = "96108b8f-2a98-4f6b-a6b6-69e04e7b7d3f";
        public const string guidDjangoEditorFactoryString = "E1B7ABDE-CDDE-4874-A8A6-5B5C7597A848";

        public static readonly Guid guidDjangoCmdSet = new Guid(guidDjangoCmdSetString);
        public static readonly Guid guidDjangoEditorFactory = new Guid(guidDjangoEditorFactoryString);
        public static readonly Guid guidVenusCmdId = new Guid("c7547851-4e3a-4e5b-9173-fa6e9c8bd82c");
        public static readonly Guid guidWebPackgeCmdId = new Guid("822e3603-e573-47d2-acf0-520e4ce641c2");
        public static readonly Guid guidWebPackageGuid = new Guid("d9a342d1-a429-4059-808a-e55ee6351f7f");
        public static readonly Guid guidWebAppCmdId = new Guid("CB26E292-901A-419c-B79D-49BD45C43929");
        public static readonly Guid guidEureka = new Guid("30947ebe-9147-45f9-96cf-401bfc671a82");  //  Microsoft.VisualStudio.Web.Eureka.dll package, includes page inspector

        public static readonly Guid guidOfficeSharePointCmdSet = new Guid("d26c976c-8ee8-4ec4-8746-f5f7702a17c5");
    }
}
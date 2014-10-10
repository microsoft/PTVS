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
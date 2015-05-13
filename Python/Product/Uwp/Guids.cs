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

namespace Microsoft.PythonTools.Uwp {
    static class GuidList {
        public const string guidUwpPkgString = "0a078d3c-15a9-47f5-8418-9ee5db43993d";
        public const string guidUwpFactoryString = "c85cbf2e-4147-4e9d-87e0-9a2fbf407f6e";
        public const string guidUwpPropertyPageString = "700c8e09-f81c-4fb8-a386-508fb48c372d";
        public static readonly Guid guidOfficeSharePointCmdSet = new Guid("d26c976c-8ee8-4ec4-8746-f5f7702a17c5");
    }
}
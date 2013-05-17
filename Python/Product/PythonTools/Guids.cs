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

namespace Microsoft.PythonTools
{
    static class GuidList
    {
        public const string guidPythonToolsPkgString =    "6dbd7c1e-1f1b-496d-ac7c-c55dae66c783";
        public const string guidPythonToolsCmdSetString = "bdfa79d2-2cd2-474a-a82a-ce8694116825";

        public static readonly Guid guidPythonToolsCmdSet = new Guid(guidPythonToolsCmdSetString);
        public static readonly Guid guidCSharpProjectPacakge = new Guid("FAE04EC1-301F-11D3-BF4B-00C04F79EFBC");
    };
}
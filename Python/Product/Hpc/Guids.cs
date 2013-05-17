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

namespace Microsoft.PythonTools.Hpc
{
    static class GuidList
    {
        public const string guidPythonHpcPkgString = "DD274D18-1D06-4306-B9EE-EFB4AB261450";
        public const string guidPythonHpcCmdSetString = "787FDD35-1AC7-45BE-A8E4-3C5C353866B8";

        public static readonly Guid guidPythonHpcCmdSet = new Guid(guidPythonHpcCmdSetString);
    }
}
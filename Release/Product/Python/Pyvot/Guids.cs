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

namespace Microsoft.PythonTools.Pyvot
{
    static class GuidList
    {
        public const string guidPyvotPkgString = "5034726D-E9C8-4E93-83D6-B61F1B515C12";
        public const string guidPyvotCmdSetString = "23C0E7C6-8A07-4F98-B181-6173AACFB230";

        public static readonly Guid guidPyvotCmdSet = new Guid(guidPyvotCmdSetString);
    };
}
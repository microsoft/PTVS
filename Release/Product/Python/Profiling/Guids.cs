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

namespace Microsoft.PythonTools.Profiling
{
    static class GuidList
    {
        public const string guidPythonProfilingPkgString = "81da0100-e6db-4783-91ea-c38c3fa1b81e";
        public const string guidPythonProfilingCmdSetString = "c6e7bafe-d4b6-4e04-85af-9c83f18d8c78";
        public const string guidEditorFactoryString = "3585dc22-81a0-409e-85ae-cae5d02d99cd";

        public static readonly Guid guidPythonProfilingCmdSet = new Guid(guidPythonProfilingCmdSetString);

        public static readonly Guid VsUIHierarchyWindow_guid = new Guid("{7D960B07-7AF8-11D0-8E5E-00A0C911005A}");
        public static readonly Guid guidEditorFactory = new Guid(guidEditorFactoryString);
    };
}
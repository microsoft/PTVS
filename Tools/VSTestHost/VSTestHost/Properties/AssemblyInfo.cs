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

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("VS Test Host")]
[assembly: AssemblyDescription("Test host adapter for Visual Studio UI tests.")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("VS Test Host")]
[assembly: AssemblyCopyright("(c) Microsoft Corporation")]
[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

[assembly: AssemblyVersion(AssemblyVersionInfo.VSVersion + ".0.1.0")]
[assembly: AssemblyFileVersion(AssemblyVersionInfo.VSVersion + ".0.1.0")]

class AssemblyVersionInfo {
#if VS10
    public const string VSVersion = "10";
#elif VS11
    public const string VSVersion = "11";
#elif VS12
    public const string VSVersion = "12";
#elif VS14
    public const string VSVersion = "14";
#endif
}


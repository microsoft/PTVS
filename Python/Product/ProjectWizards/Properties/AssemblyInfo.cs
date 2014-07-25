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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Project Wizards")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyProduct("Python Tools for Visual Studio")]
[assembly: AssemblyCopyright("© Microsoft Corporation.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("2.1.0.0")]

[assembly: ComVisible(false)]

// Need to clone these settings here for SettingsManagerCreator. We can't just
// reference the existing AssemblyVersion.cs file because that will conflict
// with our other attributes here, which need to remain fixed.
class AssemblyVersionInfo {
#if DEV10
    public const string VSVersion = "10.0";
#elif DEV11
    public const string VSVersion = "11.0";
#elif DEV12
    public const string VSVersion = "12.0";
#else
#error Unrecognized VS Version.
#endif
}

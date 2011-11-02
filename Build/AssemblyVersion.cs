// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The following assembly information is common to all Technical Computing Workbench
// assemblies.
// If you get compiler errors CS0579, "Duplicate '<attributename>' attribute", check your 
// Properties\AssemblyInfo.cs file and remove any lines duplicating the ones below.
// (See also AssemblyInfoCommon.cs in this same directory.)
#if DEV11
[assembly: AssemblyVersion("1.8.41102.0")] // Assembly version not set in stone for Dev11 builds
#else
[assembly: AssemblyVersion("1.8.40818.0")]  // 1.0 shipped w/ this version, keep it for compat
#endif
[assembly: AssemblyFileVersion("1.8.41102.0")]

class AssemblyVersionInfo {
    public const string Version = "1.8.41102.0";
}

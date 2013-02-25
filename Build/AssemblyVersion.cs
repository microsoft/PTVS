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
[assembly: AssemblyVersion("0.7.4100.000")] // Assembly version not set in stone for Dev11 builds
#else
[assembly: AssemblyVersion("2.0.50201.0")]  // We need Dev10 and Dev11 to be different versions because of installations to the GAC.  This to be fixed later
#endif
[assembly: AssemblyFileVersion("0.7.4100.000")]

class AssemblyVersionInfo {
    public const string Version = "0.7.4100.000";
}
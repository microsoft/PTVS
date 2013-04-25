// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// If you get compiler errors CS0579, "Duplicate '<attributename>' attribute", check your 
// Properties\AssemblyInfo.cs file and remove any lines duplicating the ones below.
// (See also AssemblyInfoCommon.cs in this same directory.)

[assembly: AssemblyVersion(AssemblyVersionInfo.StableVersion)]
[assembly: AssemblyFileVersion(AssemblyVersionInfo.Version)]

class AssemblyVersionInfo {
    // This version string (and the comments for StableVersion and Version)
    // should be updated manually between major releases.
    // Servicing branches should retain the value
    public const string ReleaseVersion = "2.0";
    // This version string (and the comment for StableVersion) should be
    // updated manually between minor releases.
    // Servicing branches should retain the value
    public const string MinorVersion = "1";

    // This version should never change from "4100.00"; BuildRelease.ps1
    // will replace it with a generated value.
    public const string BuildNumber = "4100.00";

#if DEV10
    public const string VSMajorVersion = "10";
    const string VSVersionSuffix = "2010";
#elif DEV11
    public const string VSMajorVersion = "11";
    const string VSVersionSuffix = "2012";
#elif DEV12
    public const string VSMajorVersion = "12";
    const string VSVersionSuffix = "2013";
#else
#error Unrecognized VS Version.
#endif

    public const string VSVersion = VSMajorVersion + ".0";

    // Defaults to "2.0.1.(2010|2012|2013)"
    public const string StableVersion = ReleaseVersion + "." + MinorVersion + "." + VSVersionSuffix;

    // Defaults to "2.0.4100.00"
    public const string Version = ReleaseVersion + "." + BuildNumber;
}

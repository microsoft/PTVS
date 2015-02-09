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

// If you get compiler errors CS0579, "Duplicate '<attributename>' attribute", check your 
// Properties\AssemblyInfo.cs file and remove any lines duplicating the ones below.
// (See also AssemblyInfoCommon.cs in this same directory.)

#if !SUPPRESS_COMMON_ASSEMBLY_VERSION
[assembly: AssemblyVersion(AssemblyVersionInfo.StableVersion)]
#endif
[assembly: AssemblyFileVersion(AssemblyVersionInfo.Version)]

class AssemblyVersionInfo {
    // This version string (and the comment for StableVersion) should be
    // updated manually between major releases (e.g. from 2.0 to 3.0).
    // Servicing branches and minor releases should retain the value.
    public const string ReleaseVersion = "2.0.2";
    
    // This version string (and the comment for Version) should be updated
    // manually between minor releases (e.g. from 2.0 to 2.1).
    // Servicing branches and prereleases should retain the value.
    public const string FileVersion = "2.2";

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
#elif DEV14
    public const string VSMajorVersion = "14";
    const string VSVersionSuffix = "2015";
#else
#error Unrecognized VS Version.
#endif

    public const string VSVersion = VSMajorVersion + ".0";

    // Defaults to "2.0.2.(2010|2012|2013|2015)"
    public const string StableVersion = ReleaseVersion + "." + VSVersionSuffix;

    // Defaults to "2.1.4100.00"
    public const string Version = FileVersion + "." + BuildNumber;
}

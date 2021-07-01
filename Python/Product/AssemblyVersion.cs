// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.


// If you get compiler errors CS0579, "Duplicate '<attributename>' attribute", check your 
// Properties\AssemblyInfo.cs file and remove any lines duplicating the ones below.
// (See also AssemblyInfoCommon.cs in this same directory.)

#if !SUPPRESS_COMMON_ASSEMBLY_VERSION
[assembly: AssemblyVersion(AssemblyVersionInfo.StableVersion)]
#endif
[assembly: AssemblyFileVersion(AssemblyVersionInfo.Version)]

class AssemblyVersionInfo
{
#if DEV15
    public const string VSMajorVersion = "15";
    public const string VSName = "2017";
#elif DEV16
    public const string VSMajorVersion = "16";
    public const string VSName = "2019";
#else
#error Unrecognized VS Version.
#endif

    public const string VSVersion = VSMajorVersion + ".0";

    // These version strings are automatically updated at build.
    public const string StableVersionPrefix = "1.0.0";
    public const string StableVersion = "1.0.0.0";
    public const string Version = "1.0.0.0";
}

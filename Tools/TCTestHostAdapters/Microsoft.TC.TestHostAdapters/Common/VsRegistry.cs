/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.Win32;
using Microsoft.TC.TestHostAdapters;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Helper class for VS registry.
    /// Used by both Host Adapter and UI side.
    /// </summary>
    internal static class VsRegistry
    {
        private const string ProcessName = "devenv.exe";
#if DEV11
        private const string TargetVSVersion = "11.0";
#else
        private const string TargetVSVersion = "10.0";
#endif

        /// <summary>
        /// Obtains all installed Visual Studio versions.
        /// </summary>
        internal static List<string> GetVersions()
        {
            List<string> versions = new List<string>();
            GetVersionsHelper(versions);
            return versions;
        }

        /// <summary>
        /// Returns max version without suffix.
        /// </summary>
        internal static string GetDefaultVersion()
        {
            return TargetVSVersion;
        }

        /// <summary>
        /// Returns location of devenv.exe on disk.
        /// </summary>
        /// <param name="registryHive">The registry hive (version) of Visual Studio to get location for.</param>
        internal static string GetVsLocation(string registryHive)
        {
            Debug.Assert(!string.IsNullOrEmpty(registryHive));

            string versionKeyName = string.Format(CultureInfo.InvariantCulture,
                @"SOFTWARE\Microsoft\VisualStudio\{0}", registryHive);
            string installDir = RegistryHelper<string>.GetValueIgnoringExceptions(
                Registry.LocalMachine, versionKeyName, "InstallDir", null);

            if (string.IsNullOrEmpty(installDir))
            {
                throw new VsIdeTestHostException(string.Format(CultureInfo.InvariantCulture, Resources.CannotFindVSInstallation, registryHive));
            }

            return Path.Combine(installDir, ProcessName);
        }

        /// <summary>
        /// Obtains installed Visual Studio versions and default version.
        /// </summary>
        /// <param name="versions">If null, this is ignored.</param>
        /// <returns>Returns default version = max version without suffix.</returns>
        private static string GetVersionsHelper(List<string> versions)
        {
            // Default is the latest version without suffix, like 10.0.
            string defaultVersion = null;
            Regex versionNoSuffixRegex = new Regex(@"^[0-9]+\.[0-9]+$");

            // Note that the version does not have to be numeric only: can be 10.0Exp.
            using (RegistryKey vsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio"))
            {
                foreach (string versionKeyName in vsKey.GetSubKeyNames())
                {
                    // If there's no InstallDir subkey we skip this key.
                    using (RegistryKey versionKey = vsKey.OpenSubKey(versionKeyName))
                    {
                        if (versionKey.GetValue("InstallDir") == null)
                        {
                            continue;
                        }
                        if (versions != null)
                        {
                            versions.Add(versionKeyName);
                        }
                    }

                    if (versionNoSuffixRegex.Match(versionKeyName).Success &&
                        string.Compare(versionKeyName, defaultVersion, StringComparison.OrdinalIgnoreCase) > 0) // null has the smallest value.
                    {
                        defaultVersion = versionKeyName;
                    }
                }
            }

            return defaultVersion;
        }
    }
}

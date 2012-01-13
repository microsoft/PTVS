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
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// KeyNames/settings for VsIdeHostAdapter and VsIde.
    /// </summary>
    internal static class RegistrySettings
    {
        /// <summary>
        /// TC VS IDE Host Adapter registry settings.
        /// There are under HKCU.
        /// </summary>
        internal const string HostAdapterRegistryKeyName = @"SOFTWARE\Microsoft\TcVsIdeTestHost";

        private const string RestartVsCounterValueName = "RestartVsCounter";         // DWORD (0/N): VS is restarted AFTER Run method, then the key value is decremented.
        private const string RegistryHiveOverrideValueName = "RegistryHiveOverride"; // string: if specified, Run config and env var are ignored.
        private const string EnableVerboseAssertionsValueName = "EnableVerboseAssertions"; // Some places where we do Debug.Fail.
        private const string BaseTimeoutValueName = "BaseTimeout";                   // int/millisecs: base of all timeouts.
        private const string BaseSleepDurationValueName = "BaseSleepDuration";       // int/millisecs: base of all sleep durations.

        private static int s_baseTimeout = 1000;        // Milliseconds.
        private static int s_baseSleepDuration = 250;   // Milliseconds.

        /// <summary>
        /// Static constructor.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static RegistrySettings()
        {
            int baseTimeoutFromRegistry = RegistryHelper<int>.GetValueIgnoringExceptions(
                Registry.CurrentUser, HostAdapterRegistryKeyName, BaseTimeoutValueName, -1);
            if (baseTimeoutFromRegistry >= 0)
            {
                s_baseTimeout = baseTimeoutFromRegistry;
            }

            int baseSleepDurationFromRegistry = RegistryHelper<int>.GetValueIgnoringExceptions(
                Registry.CurrentUser, HostAdapterRegistryKeyName, BaseSleepDurationValueName, -1);
            if (baseSleepDurationFromRegistry >= 0)
            {
                s_baseSleepDuration = baseSleepDurationFromRegistry;
            }
        }

        /// <summary>
        /// Base value used for timeouts.
        /// </summary>
        internal static int BaseTimeout
        {
            get 
            {
                return s_baseTimeout;
            }
        }

        /// <summary>
        /// Base value used for sleep durations.
        /// </summary>
        internal static int BaseSleepDuration
        {
            get
            {
                return s_baseSleepDuration;
            }
        }

        /// <summary>
        /// # of times left to restart VS.
        /// </summary>
        internal static uint RestartVsCounter
        {
            get
            {
                // Registry cannot unbox as uint, so unbox as int and then cast to uint.
                return (uint)RegistryHelper<int>.GetValueIgnoringExceptions(
                    Registry.CurrentUser,
                    RegistrySettings.HostAdapterRegistryKeyName,
                    RegistrySettings.RestartVsCounterValueName,
                    0);
            }
            set
            {
                RegistryHelper<int>.SetValueIgnoringExceptions(
                    Registry.CurrentUser,
                    RegistrySettings.HostAdapterRegistryKeyName,
                    RegistrySettings.RestartVsCounterValueName,
                    (int)value);
            }
        }

        /// <summary>
        /// The Registry Hive override value. I.e. when override is set, this returns registry hive to use.
        /// Returns null if the override is not set.
        /// Each time check registry as the value can change in time.
        /// </summary>
        internal static string RegistryHiveOverride
        {
            get
            {
                return RegistryHelper<string>.GetValueIgnoringExceptions(
                    Registry.CurrentUser, RegistrySettings.HostAdapterRegistryKeyName, RegistrySettings.RegistryHiveOverrideValueName, null);
            }
        }

        /// <summary>
        /// Specifies whether verbose assertions are enabled.
        /// Each time it checks registry as the value can change in time.
        /// </summary>
        internal static bool VerboseAssertionsEnabled
        {
            get
            {
                return RegistryHelper<bool>.GetValueIgnoringExceptions(Registry.CurrentUser, HostAdapterRegistryKeyName, EnableVerboseAssertionsValueName, false);
            }
        }
    }
}

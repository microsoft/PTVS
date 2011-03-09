/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using EnvDTE;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Provides Visual Studio context for tests running inside Visual Studio IDE.
    /// </summary>
    /// <remarks>
    /// The implementation takes advantage of the fact that hosted tests run in the same app domain.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1706:ShortAcronymsShouldBeUppercase")]
    public static class VsIdeTestHostContext
    {
        private static IServiceProvider s_serviceProvider;
        private static object s_lock = new object();
        private static DTE s_dte;

        /// <summary>
        /// Service provider to hook up to VS instance.
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get 
            {
                Debug.Assert(s_serviceProvider != null, "VsIdeTestHostContext.ServiceProvider.get: s_serviceProvider is null!");
                return s_serviceProvider; 
            }

            // This accessor should be internal.
            // The reason this is public is that assemblies in the sample are not signed.
            // If you sign your Host Adapter assembly please make this "set" accessor internal
            // and add InternalsVisibleTo attribute to this assembly to enable Host Adapter assembly access internal methods of this assembly.
            set
            {
                Debug.Assert(value != null, "VsIdeTestHostContext.ServiceProvider.set: passed value = null!");
                s_serviceProvider = value;
            }
        }

        /// <summary>
        /// Returns Visual Studio DTE.
        /// </summary>
        /// <remarks>
        /// The reason for this is GetService is not thread safe.
        /// This property is thread safe.
        /// </remarks>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dte")]  // DTE comes from VS DTE.
        [CLSCompliant(false)]
        public static DTE Dte
        {
            get
            {
                IServiceProvider serviceProvider = s_serviceProvider;   // Get snapshot of s_serviceProvider.
                if (serviceProvider == null)
                {
                    Debug.Fail("VsIdeTestHostContext.Dte: m_serviceProvider is null!");
                    return null;
                }

                if (s_dte == null)
                {
                    lock (s_lock)   // Protect GetService.
                    {
                        if (s_dte == null)
                        {
                            s_dte = (DTE)serviceProvider.GetService(typeof(DTE));
                        }
                    }
                }
                return s_dte;
            }
        }
    }
}

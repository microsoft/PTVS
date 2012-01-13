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
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Profiling {
    [Guid("C932B3FB-B9CF-4903-83CA-394E2E89C4A0")]
    public interface IPythonProfiling {
        IPythonProfileSession GetSession(object item);

        /// <summary>
        /// Launches profiling for the provided project using the projects current settings.
        /// </summary>
        IPythonProfileSession LaunchProject(EnvDTE.Project projectToProfile, bool openReport = true);

        /// <summary>
        /// Launches profiling for the provided process.  
        /// </summary>
        /// <param name="interpreter">
        /// Either a full path to an interpreter or Guid;Version where Guid is the Python interpreter
        /// guid and version is the version to run.
        /// </param>
        /// <param name="script">
        /// Path to the script to profile.
        /// </param>
        /// <param name="workingDir">Working directory to run script from.</param>
        /// <param name="arguments">Any additional arguments which should be provided to the process.</param>
        IPythonProfileSession LaunchProcess(string interpreter, string script, string workingDir, string arguments, bool openReport = true);

        void RemoveSession(IPythonProfileSession session, bool deleteFromDisk);

        bool IsProfiling {
            get;
        }
    }
}

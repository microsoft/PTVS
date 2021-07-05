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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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

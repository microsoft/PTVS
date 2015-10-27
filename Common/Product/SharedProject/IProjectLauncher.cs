// Visual Studio Shared Project
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

using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Defines an interface for launching a project or a file with or without debugging.
    /// </summary>
    public interface IProjectLauncher {
        /// <summary>
        /// Starts a project with or without debugging.
        /// </summary>
        /// <returns>HRESULT indicating success or failure.</returns>
        int LaunchProject(bool debug);

        /// <summary>
        /// Starts a file in a project with or without debugging.
        /// </summary>
        /// <returns>HRESULT indicating success or failure.</returns>
        int LaunchFile(string file, bool debug);
    }

    public interface IProjectLauncher2 : IProjectLauncher {
        /// <summary>
        /// Starts a file in a project with custom settings.
        /// </summary>
        int LaunchFile(string file, bool debug, IProjectLaunchProperties properties);
    }
}

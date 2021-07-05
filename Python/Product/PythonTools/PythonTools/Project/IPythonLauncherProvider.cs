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


using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    public interface IPythonLauncherProvider {

        /// <summary>
        /// Gets the options for the provided launcher.
        /// </summary>
        /// <returns></returns>
        IPythonLauncherOptions GetLauncherOptions(IPythonProject properties);

        /// <summary>
        /// Gets the canonical name of the launcher.
        /// </summary>
        /// <remarks>
        /// This name is used to reference the launcher from project files and
        /// should not vary with language or culture. To specify a culture-
        /// sensitive name, use
        /// <see cref="IPythonLauncherProvider2.LocalizedName"/>.
        /// </remarks>
        string Name {
            get;
        }

        /// <summary>
        /// Gets a longer description of the launcher.
        /// </summary>
        string Description {
            get;
        }

        IProjectLauncher CreateLauncher(IPythonProject project);

        /// <summary>
        /// Gets the localized name of the launcher.
        /// </summary>
        string LocalizedName {
            get;
        }

        /// <summary>
        /// Gets the sort priority of the launcher. Lower values sort earlier in
        /// user-visible lists.
        /// </summary>
        int SortPriority {
            get;
        }
    }
}

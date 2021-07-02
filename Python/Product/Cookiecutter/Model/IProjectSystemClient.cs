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

namespace Microsoft.CookiecutterTools.Model
{
    interface IProjectSystemClient
    {
        /// <summary>
        /// Returns information about the selected project node or folder node
        /// in solution explorer, or <c>null</c> if there is no selection or if the
        /// selection doesn't represent a valid folder.
        /// </summary>
        ProjectLocation GetSelectedFolderProjectLocation();

        /// <summary>
        /// Add the specified list of created folders and files to the specified
        /// project.
        /// </summary>
        /// <param name="location">
        /// The project to add to, and the path to the folder where the files
        /// were created within the project folder.
        /// </param>
        /// <param name="creationResult">
        /// Files that were created and must be added to the project.
        /// All paths are relative.
        /// </param>
        void AddToProject(ProjectLocation location, CreateFilesOperationResult creationResult);

        /// <summary>
        /// Add the specified project to the currently loaded solution.
        /// </summary>
        /// <param name="projectFilePath">Path to project file to add.</param>
        void AddToSolution(string projectFilePath);

        /// <summary>
        /// Returns whether a solution is currently open in the IDE.
        /// </summary>
        bool IsSolutionOpen { get; }

        /// <summary>
        /// Notification for when a solution is opened or closed in the IDE.
        /// </summary>
        event EventHandler SolutionOpenChanged;
    }

    class ProjectLocation
    {
        public string ProjectUniqueName { get; set; }
        public string ProjectKind { get; set; }
        public string FolderPath { get; set; }
    }
}

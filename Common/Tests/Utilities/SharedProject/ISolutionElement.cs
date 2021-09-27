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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Represents a solution element such as a project or solution folder.
    /// </summary>
    public interface ISolutionElement
    {
        /// <summary>
        /// Gets the name of the solution element
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The type guid for the project type or other solution element type such as a folder.
        /// </summary>
        Guid TypeGuid { get; }

        /// <summary>
        /// Gets the flags which control how the solution element is written to the
        /// solution file.
        /// </summary>
        SolutionElementFlags Flags { get; }

        /// <summary>
        /// Saves the solution element to disk at the specified location.  The
        /// impelementor can return the created project or null if the solution
        /// element doesn't create a project.
        /// </summary>
        MSBuild.Project Save(MSBuild.ProjectCollection collection, string location);
    }
}

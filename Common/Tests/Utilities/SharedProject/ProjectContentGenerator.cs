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
    /// Base class for all generated project items.  Override Generate to create
    /// the item on disk (relative to the MSBuild.Project) and optionally add the
    /// generated item to the project.  
    /// </summary>
    public abstract class ProjectContentGenerator
    {
        /// <summary>
        /// Generates the specified item.  The item can use the project type to 
        /// customize the item.  The item can write it's self out to disk if 
        /// necessary and update the project file appropriately.
        /// </summary>
        public abstract void Generate(ProjectType projectType, MSBuild.Project project);
    }
}

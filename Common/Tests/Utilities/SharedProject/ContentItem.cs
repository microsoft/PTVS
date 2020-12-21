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

using System.IO;
using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Generates a file and project item of type Content and if not excluded 
    /// adds it to the generated project.
    /// </summary>
    public sealed class ContentItem : ProjectContentGenerator
    {
        public readonly string Name;
        public readonly string Content;
        public readonly bool IsExcluded;

        /// <summary>
        /// Creates a new content item with the specifed name and content.
        /// 
        /// If the item is excluded the file will be created, but not added
        /// to the project.
        /// </summary>
        public ContentItem(string name, string content, bool isExcluded = false)
        {
            Name = name;
            Content = content;
            IsExcluded = isExcluded;
        }

        public override void Generate(ProjectType projectType, MSBuild.Project project)
        {
            var filename = Path.Combine(project.DirectoryPath, Name);
            File.WriteAllText(filename, Content);

            if (!IsExcluded)
            {
                project.AddItem("Content", Name);
            }
        }
    }

}

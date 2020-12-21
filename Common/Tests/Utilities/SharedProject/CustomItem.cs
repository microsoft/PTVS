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

using System.Collections.Generic;
using System.IO;
using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Generates a custom msbuild item .
    /// 
    /// The item is added to the project if not excluded.
    /// </summary>
    public sealed class CustomItem : ProjectContentGenerator
    {
        public readonly string Name, Content, ItemType;
        public readonly bool IsExcluded;
        public readonly bool IsMissing;
        public readonly IEnumerable<KeyValuePair<string, string>> Metadata;

        /// <summary>
        /// Creates a new custom item with the specifed type, name, content, and metadata.
        /// </summary>
        public CustomItem(string itemType, string name, string content = null, bool isExcluded = false, bool isMissing = false, IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            ItemType = itemType;
            Name = name;
            IsExcluded = isExcluded;
            IsMissing = isMissing;
            Content = content;
            Metadata = metadata;
        }

        public override void Generate(ProjectType projectType, MSBuild.Project project)
        {
            var filename = Path.Combine(project.DirectoryPath, Name);
            if (!IsMissing)
            {
                File.WriteAllText(filename, Content);
            }

            if (!IsExcluded)
            {
                project.AddItem(
                    ItemType,
                    Name,
                    Metadata
                );
            }
        }
    }

}

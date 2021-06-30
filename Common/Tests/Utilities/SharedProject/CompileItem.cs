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
    /// Generates a source code file.  The extension will be the code extension for
    /// the project type being generated and content will be the default content.
    /// 
    /// The item is added to the project if not excluded.
    /// </summary>
    public sealed class CompileItem : ProjectContentGenerator
    {
        public readonly string Name, Content, LinkFile;
        public readonly bool IsExcluded;
        public readonly bool IsMissing;

        /// <summary>
        /// Creates a new compile item.  The item will be generated with the 
        /// projects code file extension and sample code.  If the item is excluded
        /// then the file will be written out but not added to the project.
        /// If the item is missing then the file will not be written to disk.
        /// 
        /// If content is not provided or is null then the default sample code
        /// from the project type will be used.
        /// </summary>
        public CompileItem(string name, string content = null, bool isExcluded = false, bool isMissing = false, string link = null)
        {
            Name = name;
            IsExcluded = isExcluded;
            IsMissing = isMissing;
            Content = content;
            LinkFile = link;
        }

        public override void Generate(ProjectType projectType, MSBuild.Project project)
        {
            var filename = Path.Combine(project.DirectoryPath, Name + projectType.CodeExtension);
            if (!IsMissing)
            {
                File.WriteAllText(filename, Content ?? projectType.SampleCode);
            }

            if (!IsExcluded)
            {
                List<KeyValuePair<string, string>> metadata = new List<KeyValuePair<string, string>>();
                if (LinkFile != null)
                {
                    metadata.Add(new KeyValuePair<string, string>("Link", LinkFile + projectType.CodeExtension));
                }

                project.AddItem(
                    "Compile",
                    Name + projectType.CodeExtension,
                    metadata
                );
            }
        }

        public CompileItem Link(string link)
        {
            return new CompileItem(
                Name,
                Content,
                IsExcluded,
                IsMissing,
                link
            );
        }
    }

}

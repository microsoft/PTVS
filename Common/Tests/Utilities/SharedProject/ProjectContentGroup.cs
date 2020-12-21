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
    /// Groups a set of ProjectContentGenerator together.
    /// 
    /// This class exists solely to allow a hierarchy to be written in
    /// source code when describing the test projects.
    /// 
    /// It takes a list of ProjectContentGenerator, and when asked to
    /// generate will generate the list in order.
    /// </summary>
    public class ProjectContentGroup : ProjectContentGenerator
    {
        private readonly ProjectContentGenerator[] _content;

        public ProjectContentGroup(ProjectContentGenerator[] content)
        {
            _content = content;
        }

        public override void Generate(ProjectType projectType, MSBuild.Project project)
        {
            foreach (var content in _content)
            {
                content.Generate(projectType, project);
            }
        }
    }
}

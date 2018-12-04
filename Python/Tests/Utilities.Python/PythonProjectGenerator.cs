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

using System;
using System.Linq;
using TestUtilities.SharedProject;

namespace TestUtilities.Python {
    public class PythonProjectGenerator : ProjectGenerator {
        public PythonProjectGenerator(IServiceProvider site) : base(site) { }

        public static PythonProjectGenerator CreateStatic() {
            return new PythonProjectGenerator(
                ProjectType.FromType(typeof(PythonTestDefintions), new[] { new PythonProjectProcessor() }).ToArray()
            );
        }

        private PythonProjectGenerator(params ProjectType[] projectTypes) : base(projectTypes) {
        }

        public ProjectType PythonProject => ProjectTypes.First(x => x.ProjectExtension == ".pyproj");

        public ProjectDefinition Project(string name, params ProjectContentGenerator[] items) {
            return new ProjectDefinition(name, PythonProject, items);
        }

        public SolutionFile Generate(ProjectDefinition project) {
            return new ProjectDefinition(PythonProject, project).Generate();
        }
    }
}

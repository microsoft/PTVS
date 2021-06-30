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

using System;
using System.IO;
using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Class used to define a project.  A project consists of a type, a name, 
    /// the items in the project (which will be generated at test time) as well as
    /// MSBuild project properties.
    /// </summary>
    public sealed class ProjectDefinition : ISolutionElement
    {
        private readonly bool _isUserProject;
        public readonly ProjectType ProjectType;
        private readonly string _name;
        public readonly ProjectContentGenerator[] Items;
        public readonly Guid Guid = Guid.NewGuid();

        /// <summary>
        /// Creates a new generic project not associated with any language that can be used
        /// as a project which is imported from another project.
        /// </summary>
        public ProjectDefinition(string name, params ProjectContentGenerator[] items)
        {
            ProjectType = ProjectType.Generic;
            _name = name;
            Items = items;
        }

        /// <summary>
        /// Creates a new project definition which can be included in a solution or generated.
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="projectType">The project type which controls the language being tested</param>
        /// <param name="items">The items included in the project</param>
        public ProjectDefinition(string name, ProjectType projectType, params ProjectContentGenerator[] items)
        {
            ProjectType = projectType;
            _name = name;
            Items = items;
        }

        public ProjectDefinition(string name, ProjectType projectType, bool isUserProject, params ProjectContentGenerator[] items)
            : this(name, projectType, items)
        {
            _isUserProject = isUserProject;
        }

        public ProjectDefinition(ProjectType newProjectType, ProjectDefinition wrap)
            : this(wrap.Name, newProjectType, wrap._isUserProject, wrap.Items)
        {
        }

        /// <summary>
        /// Helper function which generates the project and solution with just this 
        /// project in the solution.
        /// </summary>
        public SolutionFile Generate()
        {
            return SolutionFile.Generate(_name, this);
        }

        public MSBuild.Project Save(MSBuild.ProjectCollection collection, string location)
        {
            location = Path.Combine(location, _name);
            Directory.CreateDirectory(location);

            var project = new MSBuild.Project(collection);
            string projectFile = Path.Combine(location, _name) + ProjectType.ProjectExtension;
            if (_isUserProject)
            {
                projectFile += ".user";
            }
            project.Save(projectFile);

            if (ProjectType != ProjectType.Generic)
            {
                var projGuid = Guid;
                project.SetProperty("ProjectTypeGuid", TypeGuid.ToString());
                project.SetProperty("Name", _name);
                project.SetProperty("ProjectGuid", projGuid.ToString("B"));
                project.SetProperty("SchemaVersion", "2.0");
                var group = project.Xml.AddPropertyGroup();
                group.Condition = " '$(Configuration)' == 'Debug' ";
                group.AddProperty("DebugSymbols", "true");
                group = project.Xml.AddPropertyGroup();
                group.Condition = " '$(Configuration)' == 'Release' ";
                group.AddProperty("DebugSymbols", "false");
            }

            foreach (var processor in ProjectType.Processors)
            {
                processor.PreProcess(project);
            }

            foreach (var item in Items)
            {
                item.Generate(ProjectType, project);
            }

            foreach (var processor in ProjectType.Processors)
            {
                processor.PostProcess(project);
            }

            project.Save();

            return project;
        }

        public Guid TypeGuid => ProjectType.ProjectTypeGuid;

        public SolutionElementFlags Flags
        {
            get
            {
                if (ProjectType == ProjectType.Generic)
                {
                    return SolutionElementFlags.ExcludeFromConfiguration |
                        SolutionElementFlags.ExcludeFromSolution;
                }
                else if (_isUserProject)
                {
                    return SolutionElementFlags.ExcludeFromSolution;
                }

                return SolutionElementFlags.None;
            }
        }

        public string Name { get { return _name; } }
    }
}

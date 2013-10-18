/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;

namespace TestUtilities.SharedProject {
    /// <summary>
    /// Class used to define a project.  A project consists of a type, a name, 
    /// the items in the project (which will be generated at test time) as well as
    /// MSBuild project properties.
    /// </summary>
    public sealed class ProjectDefinition {
        public readonly ProjectType ProjectType;
        public readonly string Name;
        public readonly ProjectContentGenerator[] Items;

        /// <summary>
        /// Creates a new project definition which can be included in a solution or generated.
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="projectType">The project type which controls the language being tested</param>
        /// <param name="items">The items included in the project</param>
        public ProjectDefinition(string name, ProjectType projectType, params ProjectContentGenerator[] items) {
            ProjectType = projectType;
            Name = name;
            Items = items;
        }

        /// <summary>
        /// Helper function which generates the project and solution with just this 
        /// project in the solution.
        /// </summary>
        public SolutionFile Generate() {
            return SolutionFile.Generate(Name, this);
        }
    }
}

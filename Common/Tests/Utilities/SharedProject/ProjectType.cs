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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject {
    /// <summary>
    /// Represents a project type.  ProjectType's can be created and exported to MEF by
    /// defining a ProjectTypeDefinition export.
    /// 
    /// The ProjectType encapsulates all the variables of a project system for a specific
    /// language.  This includes the project extension, project type guid, code file 
    /// extension, etc...
    /// </summary>
    public sealed class ProjectType {
        public readonly string CodeExtension, ProjectExtension, SampleCode;
        public readonly Guid ProjectTypeGuid;
        private readonly IProjectProcessor[] _processors;

        /// <summary>
        /// Provides a ProjectKind which will produce a C# project.  Used for multiple project solution
        /// testing scenarios.  Not exported because there's no need to test the C# project system.
        /// </summary>
        public static readonly ProjectType CSharp = new ProjectType(".cs", ".csproj", new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"), "class C { }");

        public ProjectType(string codeExtension, string projectExtension, Guid projectTypeGuid, string sampleCode = "", IProjectProcessor[] postProcess = null) {
            Debug.Assert(!String.IsNullOrWhiteSpace(codeExtension));

            CodeExtension = codeExtension;
            ProjectExtension = projectExtension;
            SampleCode = sampleCode;
            ProjectTypeGuid = projectTypeGuid;
            _processors = postProcess ?? new IProjectProcessor[0];
        }

        /// <summary>
        /// Appends the code extension to a filename
        /// </summary>
        public string Code(string filename) {
            if (String.IsNullOrWhiteSpace(filename)) {
                throw new ArgumentException("no filename suppied", "filename");
            }
            return filename + CodeExtension;
        }

        public MSBuild.Project Generate(MSBuild.ProjectCollection collection, string location, string projectName, params ProjectContentGenerator[] items) {
            location = Path.Combine(location, projectName);
            Directory.CreateDirectory(location);

            var project = new MSBuild.Project(collection);
            project.Save(Path.Combine(location, projectName) + ProjectExtension);

            var projGuid = Guid.NewGuid();
            project.SetProperty("ProjectTypeGuid", ProjectTypeGuid.ToString());
            project.SetProperty("Name", projectName);
            project.SetProperty("ProjectGuid", projGuid.ToString("B"));
            project.SetProperty("SchemaVersion", "2.0");

            foreach (var processor in _processors) {
                processor.PreProcess(project);
            }

            foreach (var item in items) {
                item.Generate(this, project);
            }

            foreach (var processor in _processors) {
                processor.PostProcess(project);
            }

            project.Save();

            return project;
        }
    }
}

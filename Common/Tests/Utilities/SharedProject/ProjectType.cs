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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
        public readonly IProjectProcessor[] Processors;

        /// <summary>
        /// Provides a ProjectType which will produce a C# project.  Used for multiple project solution
        /// testing scenarios.  Not exported because there's no need to test the C# project system.
        /// </summary>
        public static readonly ProjectType CSharp = new ProjectType(".cs", ".csproj", new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"), "class C { }");

        /// <summary>
        /// Provides a ProjectType which is completely generic.  Useful for generating a simple
        /// .proj file which will be imported fropm another project.
        /// </summary>
        public static readonly ProjectType Generic = new ProjectType(".txt", ".proj", Guid.Empty, "");

        public ProjectType(string codeExtension, string projectExtension, Guid projectTypeGuid, string sampleCode = "", IProjectProcessor[] postProcess = null) {
            Debug.Assert(!String.IsNullOrWhiteSpace(codeExtension));

            CodeExtension = codeExtension;
            ProjectExtension = projectExtension;
            SampleCode = sampleCode;
            ProjectTypeGuid = projectTypeGuid;
            Processors = postProcess ?? new IProjectProcessor[0];
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
    }
}

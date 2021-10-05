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

using Microsoft.VisualStudioTools;

using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Represents a generic solution which can be generated for shared project tests based upon
    /// the language which is being tested.
    /// 
    /// Call Solution.Generate to write the solution out to disk and return an IDisposable object
    /// which when disposed will clean up the solution.
    /// 
    /// You can also get a SolutionFile by calling ProjectDefinition.Generate which will create
    /// a single project SolutionFile.
    /// </summary>
    public sealed class SolutionFile : IDisposable
    {
        public readonly string Filename;
        public readonly ISolutionElement[] Projects;

        private SolutionFile(string slnFilename, ISolutionElement[] projects)
        {
            Filename = slnFilename;
            Projects = projects;
        }

        public static SolutionFile Generate(string solutionName, params ISolutionElement[] toGenerate)
        {
            return Generate(solutionName, -1, toGenerate);
        }

        /// <summary>
        /// Generates the solution file with the specified amount of space remaining relative
        /// to MAX_PATH.
        /// </summary>
        /// <param name="solutionName">The solution name to be created</param>
        /// <param name="pathSpaceRemaining">The amount of path space remaining, or -1 to generate normally</param>
        /// <param name="toGenerate">The projects to be incldued in the generated solution</param>
        /// <returns></returns>
        public static SolutionFile Generate(string solutionName, int pathSpaceRemaining, params ISolutionElement[] toGenerate)
        {
            List<MSBuild.Project> projects = new List<MSBuild.Project>();
            var location = TestData.GetTempPath();

            if (pathSpaceRemaining >= 0)
            {
                int targetPathLength = 260 - pathSpaceRemaining;
                location = location + new string('X', targetPathLength - location.Length);
            }
            System.IO.Directory.CreateDirectory(location);

            MSBuild.ProjectCollection collection = new MSBuild.ProjectCollection();
            // VisualStudioVersion property may not be set in mock tests
            collection.SetGlobalProperty("VisualStudioVersion", AssemblyVersionInfo.VSVersion);
            foreach (var project in toGenerate)
            {
                projects.Add(project.Save(collection, location));
            }

#if DEV10
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 11.00\r\n\u0023 Visual Studio 2010\r\n");
#elif DEV11
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n\u0023 Visual Studio 2012\r\n");
#elif DEV12
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n\u0023 Visual Studio 2013\r\nVisualStudioVersion = 12.0.20827.3\r\nMinimumVisualStudioVersion = 10.0.40219.1\r\n");
#elif DEV14
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n\u0023 Visual Studio 14\r\nVisualStudioVersion = 14.0.25123.0\r\nMinimumVisualStudioVersion = 10.0.40219.1\r\n");
#elif DEV15
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n\u0023 Visual Studio 15\r\nVisualStudioVersion = 15.0.25424.0\r\nMinimumVisualStudioVersion = 10.0.40219.1\r\n");
#elif DEV16
            StringBuilder slnFile = new StringBuilder("\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n\u0023 Visual Studio 15\r\nVisualStudioVersion = 16.0.0.0\r\nMinimumVisualStudioVersion = 10.0.40219.1\r\n");
#else
#error Unsupported VS version
#endif
            for (int i = 0; i < projects.Count; i++)
            {
                if (toGenerate[i].Flags.HasFlag(SolutionElementFlags.ExcludeFromSolution))
                {
                    continue;
                }

                var project = projects[i];
                var projectTypeGuid = toGenerate[i].TypeGuid;

                slnFile.AppendFormat(@"Project(""{0}"") = ""{1}"", ""{2}"", ""{3}""
EndProject
", projectTypeGuid.ToString("B").ToUpperInvariant(),
 project != null ? Path.GetFileNameWithoutExtension(project.FullPath) : toGenerate[i].Name,
 project != null ? CommonUtils.GetRelativeFilePath(location, project.FullPath) : toGenerate[i].Name,
 (project != null ? Guid.Parse(project.GetProperty("ProjectGuid").EvaluatedValue) : Guid.NewGuid()).ToString("B").ToUpperInvariant()
 );
            }
            slnFile.Append(@"Global
\tGlobalSection(SolutionConfigurationPlatforms) = preSolution
\t\tDebug|Any CPU = Debug|Any CPU
\t\tRelease|Any CPU = Release|Any CPU
\tEndGlobalSection
\tGlobalSection(ProjectConfigurationPlatforms) = postSolution
");
            for (int i = 0; i < projects.Count; i++)
            {
                if (toGenerate[i].Flags.HasFlag(SolutionElementFlags.ExcludeFromConfiguration))
                {
                    continue;
                }

                var project = projects[i];
                slnFile.AppendFormat(@"\t\t{0:B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
\t\t{0}.Debug|Any CPU.Build.0 = Debug|Any CPU
\t\t{0}.Release|Any CPU.ActiveCfg = Release|Any CPU
\t\t{0}.Release|Any CPU.Build.0 = Release|Any CPU
", Guid.Parse(project.GetProperty("ProjectGuid").EvaluatedValue).ToString("B").ToUpperInvariant());
            }

            slnFile.Append(@"\tEndGlobalSection
\tGlobalSection(SolutionProperties) = preSolution
\t\tHideSolutionNode = FALSE
\tEndGlobalSection
EndGlobal
");

            collection.UnloadAllProjects();
            collection.Dispose();

            // MSBuild.Project doesn't want to save ToolsVersion as 4.0,
            // (passing it to MSBuild.Project ctor does nothing)
            // so manually replace it here.
            foreach (var proj in projects)
            {
                var text = File.ReadAllText(proj.FullPath, Encoding.UTF8);
                text = text.Replace("ToolsVersion=\"Current\"", "ToolsVersion=\"4.0\"");
                File.WriteAllText(proj.FullPath, text, Encoding.UTF8);
            }

            var slnFilename = Path.Combine(location, solutionName + ".sln");
            File.WriteAllText(slnFilename, slnFile.ToString().Replace("\\t", "\t"), Encoding.UTF8);
            return new SolutionFile(slnFilename, toGenerate);
        }

        public string Directory
        {
            get
            {
                return Path.GetDirectoryName(Filename);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }
}

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

namespace TestUtilities.SharedProject
{
    /// <summary>
    /// Base class for all test cases which generate projects at runtime in a language
    /// agnostic way.  This class will initialize the MEF catalog and get the various
    /// project kind(s) to be tested.  The kinds wil be available via the ProjectKinds
    /// property.
    /// 
    /// It also provides a number of convenience methods for creating project definitions.
    /// This helps to make the project definition more readable and similar to typical
    /// MSBuild structure.
    /// </summary>
    public class ProjectGenerator
    {
        public IEnumerable<ProjectType> ProjectTypes { get; set; }

        public ProjectGenerator(IServiceProvider site)
        {
            // Initialize our ProjectTypes information from the catalog
            var container = ((IComponentModel)site.GetService(typeof(SComponentModel))).DefaultExportProvider;

            // First, get a mapping from extension type to all available IProjectProcessor's for
            // that extension
            var processorsMap = container
                .GetExports<IProjectProcessor, IProjectProcessorMetadata>()
                .GroupBy(x => x.Metadata.ProjectExtension)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(lazy => lazy.Value).ToArray(),
                    StringComparer.OrdinalIgnoreCase
                );

            // Then create the ProjectTypes
            ProjectTypes = container
                .GetExports<ProjectTypeDefinition, IProjectTypeDefinitionMetadata>()
                .Select(lazyVal =>
                {
                    var md = lazyVal.Metadata;
                    IProjectProcessor[] processors;
                    processorsMap.TryGetValue(md.ProjectExtension, out processors);

                    return new ProjectType(
                        md.CodeExtension,
                        md.ProjectExtension,
                        Guid.Parse(md.ProjectTypeGuid),
                        md.SampleCode,
                        processors
                    );
                });

            // something's broken if we don't have any languages to test against, so fail the test.
            Assert.IsTrue(ProjectTypes.Count() > 0, "no project types were registered and no tests will run");
        }

        public ProjectGenerator(params ProjectType[] types)
        {
            ProjectTypes = types.ToArray();
        }

        /// <summary>
        /// Helper function to create a ProjectProperty object to simply syntax in 
        /// project definitions.
        /// </summary>
        public static ProjectProperty Property(string name, string value)
        {
            return new ProjectProperty(name, value);
        }

        /// <summary>
        /// Helper function to create a StartupFileProjectProperty object to simply syntax in 
        /// project definitions.
        /// </summary>
        public static StartupFileProjectProperty StartupFile(string filename)
        {
            return new StartupFileProjectProperty(filename);
        }

        /// <summary>
        /// Helper function to create a group of properties when creating project definitions.
        /// These aren't strictly necessary and just serve to add structure to the code
        /// and make it similar to an MSBuild project file.
        /// </summary>
        public static ProjectContentGroup PropertyGroup(params ProjectProperty[] properties)
        {
            return new ProjectContentGroup(properties);
        }

        /// <summary>
        /// Helper function to create a CompileItem object to simply syntax in 
        /// defining project definitions.
        /// </summary>
        public static CompileItem Compile(string name, string content = null, bool isExcluded = false, bool isMissing = false)
        {
            return new CompileItem(name, content, isExcluded, isMissing);
        }

        /// <summary>
        /// Helper function to create a CompileItem object to simply syntax in 
        /// defining project definitions.
        /// </summary>
        public static ContentItem Content(string name, string content, bool isExcluded = false)
        {
            return new ContentItem(name, content, isExcluded);
        }

        /// <summary>
        /// Helper function to create a SymbolicLinkItem object to simply syntax in 
        /// defining project definitions.
        /// </summary>
        public static SymbolicLinkItem SymbolicLink(string name, string referencePath, bool isExcluded = false, bool isMissing = false)
        {
            return new SymbolicLinkItem(name, referencePath, isExcluded, isMissing);
        }

        /// <summary>
        /// Helper function to create a FolderItem object to simply syntax in 
        /// defining project definitions.
        /// </summary>
        public static FolderItem Folder(string name, bool isExcluded = false, bool isMissing = false)
        {
            return new FolderItem(name, isExcluded, isMissing);
        }

        /// <summary>
        /// Helper function to create a CustomItem object which is an MSBuild item with
        /// the specified item type.
        /// </summary>
        public static CustomItem CustomItem(string itemType, string name, string content = null, bool isExcluded = false, bool isMissing = false, IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            return new CustomItem(itemType, name, content, isExcluded, isMissing, metadata);
        }

        /// <summary>
        /// Helper function to create a group of items when creating project definitions.
        /// These aren't strictly necessary and just serve to add structure to the code
        /// and make it similar to an MSBuild project file.
        /// </summary>
        public static ProjectContentGroup ItemGroup(params ProjectContentGenerator[] properties)
        {
            return new ProjectContentGroup(properties);
        }

        /// <summary>
        /// Returns a new SolutionFolder object which can be used to create
        /// a solution folder in the generated project.
        /// </summary>
        public static SolutionFolder SolutionFolder(string name)
        {
            return new SolutionFolder(name);
        }

        /// <summary>
        /// Returns a new TargetDefinition which represents a specified Target
        /// inside of the project file.  The various stages of the target can be
        /// created using the members of the static Tasks class.
        /// </summary>
        public static TargetDefinition Target(string name, params Action<ProjectTargetElement>[] creators)
        {
            return new TargetDefinition(name, creators);
        }

        /// <summary>
        /// Returns a new TargetDefinition which represents a specified Target
        /// inside of the project file.  The various stages of the target can be
        /// created using the members of the static Tasks class.
        /// </summary>
        public static TargetDefinition Target(string name, string dependsOnTargets, params Action<ProjectTargetElement>[] creators)
        {
            return new TargetDefinition(name, creators) { DependsOnTargets = dependsOnTargets };
        }

        /// <summary>
        /// Returns a new ImportDefinition for the specified project.
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        public static ImportDefinition Import(string project)
        {
            return new ImportDefinition(project);
        }

        /// <summary>
        /// Provides tasks for creating target definitions in generated projects.
        /// </summary>
        public static class Tasks
        {
            /// <summary>
            /// Creates a task which outputs a message during the build.
            /// </summary>
            public static Action<ProjectTargetElement> Message(string message, string importance = null)
            {
                return target =>
                {
                    var messageTask = target.AddTask("Message");
                    messageTask.SetParameter("Text", message);
                    if (importance != null)
                    {
                        messageTask.SetParameter("Importance", importance);
                    }
                };
            }
        }
    }
}

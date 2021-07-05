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

namespace Microsoft.PythonTools.Project.ImportWizard {
    abstract class ProjectCustomization {
        public abstract string DisplayName {
            get;
        }

        public override string ToString() {
            return DisplayName;
        }

        public abstract void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        );

        protected static void AddOrSetProperty(ProjectRootElement project, string name, string value) {
            bool anySet = false;
            foreach (var prop in project.Properties.Where(p => p.Name == name)) {
                prop.Value = value;
                anySet = true;
            }

            if (!anySet) {
                project.AddProperty(name, value);
            }
        }

        protected static void AddOrSetProperty(ProjectPropertyGroupElement group, string name, string value) {
            bool anySet = false;
            foreach (var prop in group.Properties.Where(p => p.Name == name)) {
                prop.Value = value;
                anySet = true;
            }

            if (!anySet) {
                group.AddProperty(name, value);
            }
        }
    }

    class DefaultProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new DefaultProjectCustomization();

        private DefaultProjectCustomization() { }

        public override string DisplayName {
            get {
                return Strings.ImportWizardDefaultProjectCustomization;
            }
        }

        public override void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.targets");
        }
    }

    class BottleProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new BottleProjectCustomization();

        private BottleProjectCustomization() { }

        public override string DisplayName {
            get {
                return Strings.ImportWizardBottleProjectCustomization;
            }
        }

        public override void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{e614c764-6d9e-4607-9337-b7073809a0bd};{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);
            AddOrSetProperty(globals, "WebBrowserUrl", "http://localhost");
            AddOrSetProperty(globals, "PythonDebugWebServerCommandArguments", "--debug $(CommandLineArguments)");
            AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app()");

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");

            GenericWebProjectCustomization.AddWebProjectExtensions(project);
        }
    }

    class DjangoProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new DjangoProjectCustomization();

        private DjangoProjectCustomization() { }

        public override string DisplayName {
            get {
                return Strings.ImportWizardDjangoProjectCustomization;
            }
        }

        public override void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "StartupFile", "manage.py");
            AddOrSetProperty(globals, "ProjectTypeGuids", "{5F0BE9CA-D677-4A4D-8806-6076C0FAAD37};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", "Django launcher");
            AddOrSetProperty(globals, "WebBrowserUrl", "http://localhost");

            var settingsFilePath = PathUtils.FindFile(sourcePath, "settings.py", depthLimit: 1);
            if (File.Exists(settingsFilePath)) {
                var packageName = PathUtils.GetLastDirectoryName(settingsFilePath);
                AddOrSetProperty(globals, "DjangoSettingsModule", "{0}.settings".FormatInvariant(packageName));
            }

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Django.targets");

            GenericWebProjectCustomization.AddWebProjectExtensions(project);
        }
    }

    class FlaskProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new FlaskProjectCustomization();

        private FlaskProjectCustomization() { }

        public override string DisplayName {
            get {
                return Strings.ImportWizardFlaskProjectCustomization;
            }
        }

        public override void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{789894c7-04a9-4a11-a6b5-3f4435165112};{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);
            AddOrSetProperty(globals, "WebBrowserUrl", "http://localhost");
            AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app");

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");

            GenericWebProjectCustomization.AddWebProjectExtensions(project);
        }
    }

    class GenericWebProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new GenericWebProjectCustomization();

        private GenericWebProjectCustomization() { }

        public override string DisplayName {
            get {
                return Strings.ImportWizardGenericWebProjectCustomization;
            }
        }

        public override void Process(
            string sourcePath,
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);
            AddOrSetProperty(globals, "WebBrowserUrl", "http://localhost");

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");

            GenericWebProjectCustomization.AddWebProjectExtensions(project);
        }

        internal static void AddWebProjectExtensions(ProjectRootElement project) {
            // Adding this section prevents IIS Express required error message
            var projExt = project.CreateProjectExtensionsElement();
            project.AppendChild(projExt);

            projExt["VisualStudio"] = @"
    <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
    <WebProjectProperties>
        <AutoAssignPort>True</AutoAssignPort>
        <UseCustomServer>True</UseCustomServer>
        <CustomServerUrl>http://localhost</CustomServerUrl>
        <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
    </WebProjectProperties>
    </FlavorProperties>
    <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"" User="""">
    <WebProjectProperties>
        <StartPageUrl>
        </StartPageUrl>
        <StartAction>CurrentPage</StartAction>
        <AspNetDebugging>True</AspNetDebugging>
        <SilverlightDebugging>False</SilverlightDebugging>
        <NativeDebugging>False</NativeDebugging>
        <SQLDebugging>False</SQLDebugging>
        <ExternalProgram>
        </ExternalProgram>
        <StartExternalURL>
        </StartExternalURL>
        <StartCmdLineArguments>
        </StartCmdLineArguments>
        <StartWorkingDirectory>
        </StartWorkingDirectory>
        <EnableENC>False</EnableENC>
        <AlwaysStartWebServerOnDebug>False</AlwaysStartWebServerOnDebug>
    </WebProjectProperties>
    </FlavorProperties>
";
        }
    }
}

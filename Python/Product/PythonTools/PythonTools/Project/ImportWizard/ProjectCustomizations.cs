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
using System.Linq;
using Microsoft.Build.Construction;

namespace Microsoft.PythonTools.Project.ImportWizard {
    abstract class ProjectCustomization {
        public abstract string DisplayName {
            get;
        }

        public override string ToString() {
            return DisplayName;
        }

        public abstract void Process(
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
                return SR.GetString(SR.ImportWizardDefaultProjectCustomization);
            }
        }

        public override void Process(
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement imports;
            if (!groups.TryGetValue("Imports", out imports)) {
                imports = project.AddPropertyGroup();
            }

            AddOrSetProperty(imports, "PtvsTargetsFile", @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.targets");
            project.AddImport("$(PtvsTargetsFile)").Condition = "Exists($(PtvsTargetsFile))";
            project.AddImport(@"$(MSBuildToolsPath)\Microsoft.Common.targets").Condition = "!Exists($(PtvsTargetsFile))";
        }
    }

    class BottleProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new BottleProjectCustomization();

        private BottleProjectCustomization() { }

        public override string DisplayName {
            get {
                return SR.GetString(SR.ImportWizardBottleProjectCustomization);
            }
        }

        public override void Process(
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{e614c764-6d9e-4607-9337-b7073809a0bd};{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);
            AddOrSetProperty(globals, "PythonDebugWebServerCommandArguments", "--debug $(CommandLineArguments)");
            AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app()");

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");
        }
    }

    class DjangoProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new DjangoProjectCustomization();

        private DjangoProjectCustomization() { }

        public override string DisplayName {
            get {
                return SR.GetString(SR.ImportWizardDjangoProjectCustomization);
            }
        }

        public override void Process(
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

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Django.targets");
        }
    }

    class FlaskProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new FlaskProjectCustomization();

        private FlaskProjectCustomization() { }

        public override string DisplayName {
            get {
                return SR.GetString(SR.ImportWizardFlaskProjectCustomization);
            }
        }

        public override void Process(
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{789894c7-04a9-4a11-a6b5-3f4435165112};{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);
            AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app");

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");
        }
    }

    class GenericWebProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new GenericWebProjectCustomization();

        private GenericWebProjectCustomization() { }

        public override string DisplayName {
            get {
                return SR.GetString(SR.ImportWizardGenericWebProjectCustomization);
            }
        }

        public override void Process(
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{1b580a1a-fdb3-4b32-83e1-6407eb2722e6};{349c5851-65df-11da-9384-00065b846f21};{888888a0-9f3d-457c-b088-3a5042f75d52}");
            AddOrSetProperty(globals, "LaunchProvider", PythonConstants.WebLauncherName);

            project.AddImport(@"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets");
        }
    }

    class UwpProjectCustomization : ProjectCustomization {
        public static readonly ProjectCustomization Instance = new UwpProjectCustomization();

        private UwpProjectCustomization() { }

        public override string DisplayName {
            get {
                return SR.GetString(SR.ImportWizardUwpProjectCustomization);
            }
        }

        public override void Process(
            ProjectRootElement project,
            Dictionary<string, ProjectPropertyGroupElement> groups
        ) {
            ProjectPropertyGroupElement globals;
            if (!groups.TryGetValue("Globals", out globals)) {
                globals = project.AddPropertyGroup();
            }

            AddOrSetProperty(globals, "ProjectTypeGuids", "{c85cbf2e-4147-4e9d-87e0-9a2fbf407f6e};{888888A0-9F3D-457C-B088-3A5042F75D52}");
        }
    }
}

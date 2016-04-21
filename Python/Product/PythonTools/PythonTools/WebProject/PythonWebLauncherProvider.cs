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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.BuildTasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    [Export(typeof(IPythonLauncherProvider))]
    class PythonWebLauncherProvider : IPythonLauncherProvider2 {
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        private static readonly Regex SubstitutionPattern = new Regex(@"\{([\w_]+)\}");

        [ImportingConstructor]
        public PythonWebLauncherProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new PythonWebLauncherOptions(properties);
        }

        public string Name {
            get {
                return PythonConstants.WebLauncherName;
            }
        }

        public string LocalizedName {
            get {
                return Strings.PythonWebLauncherName;
            }
        }

        public string Description {
            get {
                return Strings.PythonWebLauncherDescription;
            }
        }

        public int SortPriority {
            get {
                return 100;
            }
        }

        internal static string DoSubstitutions(IPythonProject project, string str) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            return SubstitutionPattern.Replace(
                str,
                m => {
                    switch (m.Groups[1].Value.ToLowerInvariant()) {
                        case "startupfile":
                            return project.GetProperty(PythonConstants.StartupFileSetting);
                        case "startupmodule":
                            try {
                                return ModulePath.FromFullPath(
                                    PathUtils.GetAbsoluteFilePath(
                                        project.ProjectHome,
                                        project.GetProperty(PythonConstants.StartupFileSetting)
                                    ),
                                    project.ProjectHome
                                ).ModuleName;
                            } catch (ArgumentException) {
                            }
                            break;
                    }
                    return m.Value;
                }
            );
        }

        private static LaunchConfiguration GetMSBuildCommandConfig(
            LaunchConfiguration original,
            IPythonProject project,
            string targetProperty,
            string targetTypeProperty,
            string argumentsProperty,
            string environmentProperty
        ) {
            var target = DoSubstitutions(project, project.GetProperty(targetProperty));
            if (string.IsNullOrEmpty(target)) {
                return original;
            }

            var targetType = project.GetProperty(targetTypeProperty);
            if (string.IsNullOrEmpty(targetType)) {
                return original;
            }

            var config = original.Clone();
            if (PythonCommandTask.TargetTypeModule.Equals(targetType, StringComparison.OrdinalIgnoreCase)) {
                config.InterpreterArguments = (config.InterpreterArguments ?? "") + " -m " + target;
            } else if (PythonCommandTask.TargetTypeExecutable.Equals(targetType, StringComparison.OrdinalIgnoreCase)) {
                config.InterpreterPath = target;
            } else if (PythonCommandTask.TargetTypeScript.Equals(targetType, StringComparison.OrdinalIgnoreCase)) {
                config.ScriptName = target;
            }

            var args = DoSubstitutions(project, project.GetProperty(argumentsProperty));
            if (!string.IsNullOrEmpty(args)) {
                config.ScriptArguments = (config.ScriptArguments ?? "") + " " + args;
            }

            var env = DoSubstitutions(project, project.GetProperty(environmentProperty));
            config.Environment = PathUtils.MergeEnvironments(config.Environment, PathUtils.ParseEnvironment(env));

            return config;
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            var defaultConfig = project.GetLaunchConfigurationOrThrow();

            var runConfig = GetMSBuildCommandConfig(
                defaultConfig,
                project,
                PythonWebLauncher.RunWebServerTargetProperty,
                PythonWebLauncher.RunWebServerTargetTypeProperty,
                PythonWebLauncher.RunWebServerArgumentsProperty,
                PythonWebLauncher.RunWebServerEnvironmentProperty
            );
            var debugConfig = GetMSBuildCommandConfig(
                defaultConfig,
                project,
                PythonWebLauncher.DebugWebServerTargetProperty,
                PythonWebLauncher.DebugWebServerTargetTypeProperty,
                PythonWebLauncher.DebugWebServerArgumentsProperty,
                PythonWebLauncher.DebugWebServerEnvironmentProperty
            );

            // Check project type GUID and enable the Django-specific features
            // of the debugger if required.
            var projectGuids = project.GetUnevaluatedProperty("ProjectTypeGuids") ?? "";
            // HACK: Literal GUID string to avoid introducing Django-specific public API
            // We don't want to expose a constant from PythonTools.dll.
            // TODO: Add generic breakpoint extension point
            // to avoid having to pass this property for Django and any future
            // extensions.
            if (projectGuids.IndexOf("5F0BE9CA-D677-4A4D-8806-6076C0FAAD37", StringComparison.OrdinalIgnoreCase) >= 0) {
                debugConfig.LaunchOptions["DjangoDebug"] = "true";
                defaultConfig.LaunchOptions["DjangoDebug"] = "true";
            }

            return new PythonWebLauncher(_serviceProvider, runConfig, debugConfig, defaultConfig);
        }
    }
}

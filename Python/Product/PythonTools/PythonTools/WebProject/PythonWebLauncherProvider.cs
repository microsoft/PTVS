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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web
{
    [Export(typeof(IPythonLauncherProvider))]
    class PythonWebLauncherProvider : IPythonLauncherProvider
    {
        private readonly IServiceProvider _serviceProvider;

        private static readonly Regex SubstitutionPattern = new Regex(@"\{([\w_]+)\}");

        [ImportingConstructor]
        public PythonWebLauncherProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties)
        {
            return new PythonWebLauncherOptions(properties);
        }

        public string Name => PythonConstants.WebLauncherName;
        public string LocalizedName => Strings.PythonWebLauncherName;
        public string Description => Strings.PythonWebLauncherDescription;
        public int SortPriority => 100;

        internal static string DoSubstitutions(LaunchConfiguration original, IPythonProject project, string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            return SubstitutionPattern.Replace(
                str,
                m =>
                {
                    switch (m.Groups[1].Value.ToLowerInvariant())
                    {
                        case "startupfile":
                            return original.ScriptName;
                        case "startupmodule":
                            try
                            {
                                return ModulePath.FromFullPath(original.ScriptName, project.ProjectHome).ModuleName;
                            }
                            catch (ArgumentException)
                            {
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
        )
        {
            var target = DoSubstitutions(original, project, project.GetProperty(targetProperty));
            if (string.IsNullOrEmpty(target))
            {
                target = original.ScriptName;
            }

            var targetType = project.GetProperty(targetTypeProperty);
            if (string.IsNullOrEmpty(targetType))
            {
                targetType = PythonCommandTask.TargetTypeScript;
            }

            var config = original.Clone();
            if (PythonCommandTask.TargetTypeModule.Equals(targetType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(config.InterpreterArguments))
                {
                    config.InterpreterArguments = "-m " + target;
                }
                else
                {
                    config.InterpreterArguments = config.InterpreterArguments + " -m " + target;
                }
            }
            else if (PythonCommandTask.TargetTypeExecutable.Equals(targetType, StringComparison.OrdinalIgnoreCase))
            {
                config.InterpreterPath = target;
            }
            else
            {
                config.ScriptName = target;
            }

            var args = DoSubstitutions(original, project, project.GetProperty(argumentsProperty));
            if (!string.IsNullOrEmpty(args))
            {
                if (string.IsNullOrEmpty(config.ScriptArguments))
                {
                    config.ScriptArguments = args;
                }
                else
                {
                    config.ScriptArguments = args + " " + config.ScriptArguments;
                }
            }

            var env = DoSubstitutions(original, project, project.GetProperty(environmentProperty));
            config.Environment = PathUtils.MergeEnvironments(config.Environment, PathUtils.ParseEnvironment(env));

            return config;
        }

        public IProjectLauncher CreateLauncher(IPythonProject project)
        {
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
            if (projectGuids.IndexOfOrdinal("5F0BE9CA-D677-4A4D-8806-6076C0FAAD37", ignoreCase: true) >= 0)
            {
                debugConfig.LaunchOptions["DjangoDebug"] = "true";
                defaultConfig.LaunchOptions["DjangoDebug"] = "true";
            }

            return new PythonWebLauncher(_serviceProvider, runConfig, debugConfig, defaultConfig);
        }
    }
}

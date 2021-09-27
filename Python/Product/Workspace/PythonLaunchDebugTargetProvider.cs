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

namespace Microsoft.PythonTools.Workspace
{
    [ExportLaunchDebugTarget(
        ProviderType,
        new[] { PythonConstants.FileExtension, PythonConstants.WindowsFileExtension }
    )]
    class PythonLaunchDebugTargetProvider : ILaunchDebugTargetProvider
    {
        private const string ProviderType = "F2B8B667-3D13-4E51-B067-00C188D0EB7E";

        public const string LaunchTypeName = "python";

        // Set by the workspace, not by our users
        private const string ScriptNameKey = "target";

        public const string InterpreterKey = "interpreter";
        public const string InterpreterArgumentsKey = "interpreterArguments";
        public const string ScriptArgumentsKey = "scriptArguments";
        public const string EnvKey = "env";
        public const string WorkingDirectoryKey = "workingDirectory";
        public const string NativeDebuggingKey = "nativeDebug";
        public const string WebBrowserUrlKey = "webBrowserUrl";

        public const string DefaultInterpreterValue = "(default)";

        public const string JsonSchema = @"{
  ""definitions"": {
    ""python"": {
      ""type"": ""object"",
      ""properties"": {
        ""type"": {""type"": ""string"", ""enum"": [ ""python"" ]},
        ""interpreter"": { ""type"": ""string"" },
        ""interpreterArguments"": { ""type"": ""string"" },
        ""scriptArguments"": { ""type"": ""string"" },
        ""workingDirectory"": { ""type"": ""string"" },
        ""env"": { ""type"": ""object"" },
        ""nativeDebug"": { ""type"": ""boolean"" },
        ""webBrowserUrl"": { ""type"": ""string"" }
      }
    },
    ""pythonFile"": {
      ""allOf"": [
        { ""$ref"": ""#/definitions/default"" },
        { ""$ref"": ""#/definitions/python"" }
      ]
    }
  },
    ""defaults"": {
        "".py"": { ""$ref"": ""#/definitions/python"" },
        "".pyw"": { ""$ref"": ""#/definitions/python"" }
    },
    ""configuration"": ""#/definitions/pythonFile""
}";

        public void LaunchDebugTarget(IWorkspace workspace, IServiceProvider serviceProvider, DebugLaunchActionContext debugLaunchActionContext)
        {
            var registry = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();

            var settings = debugLaunchActionContext.LaunchConfiguration;
            var scriptName = settings.GetValue(ScriptNameKey, string.Empty);
            var debug = !settings.GetValue("noDebug", false);
            var interpreterVal = settings.GetValue(InterpreterKey, string.Empty);
            var path = interpreterVal;
            InterpreterConfiguration config = null;

            if (string.IsNullOrEmpty(scriptName))
            {
                throw new InvalidOperationException(Strings.DebugLaunchScriptNameMissing);
            }

            if (!string.IsNullOrEmpty(path) && !DefaultInterpreterValue.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                if (PathUtils.IsValidPath(path) && !Path.IsPathRooted(path))
                {
                    path = workspace.MakeRooted(path);
                }

                if (File.Exists(path))
                {
                    config = registry.Configurations.FirstOrDefault(c => c.InterpreterPath.Equals(path, StringComparison.OrdinalIgnoreCase)) ??
                        new VisualStudioInterpreterConfiguration("Custom", path, PathUtils.GetParent(path), path);
                }
                else
                {
                    config = registry.FindConfiguration(interpreterVal);
                }
            }
            else
            {
                var options = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                var interpreter = workspace.GetInterpreterFactory(registry, options);
                interpreter.ThrowIfNotRunnable();
                config = interpreter.Configuration;
                path = config.InterpreterPath;
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(Strings.DebugLaunchInterpreterMissing_Path.FormatUI(path));
            }

            var searchPaths = workspace.GetAbsoluteSearchPaths().ToList();

            var environment = new Dictionary<string, string>();
            if (settings.TryGetValue<IPropertySettings>(EnvKey, out IPropertySettings envSettings))
            {
                foreach (var keyVal in envSettings)
                {
                    environment[keyVal.Key] = keyVal.Value.ToString();
                }
            }

            string workingDir = settings.GetValue(WorkingDirectoryKey, string.Empty);
            if (string.IsNullOrEmpty(workingDir))
            {
                workingDir = workspace.MakeRooted(".");
            }
            else if (PathUtils.IsValidPath(workingDir) && !Path.IsPathRooted(workingDir))
            {
                workingDir = workspace.MakeRooted(workingDir);
            }

            var launchConfig = new LaunchConfiguration(config)
            {
                InterpreterPath = config == null ? path : null,
                InterpreterArguments = settings.GetValue(InterpreterArgumentsKey, string.Empty),
                ScriptName = Path.IsPathRooted(scriptName) ? scriptName : Path.Combine(workingDir, scriptName),
                ScriptArguments = settings.GetValue(ScriptArgumentsKey, string.Empty),
                WorkingDirectory = workingDir,
                SearchPaths = searchPaths,
                Environment = environment,
            };
            launchConfig.LaunchOptions[PythonConstants.EnableNativeCodeDebugging] = settings.GetValue(NativeDebuggingKey, false).ToString();

            IProjectLauncher launcher = null;
            var browserUrl = settings.GetValue(WebBrowserUrlKey, string.Empty);
            if (!string.IsNullOrEmpty(browserUrl))
            {
                launchConfig.LaunchOptions[PythonConstants.WebBrowserUrlSetting] = browserUrl;
                launcher = new PythonWebLauncher(serviceProvider, launchConfig, launchConfig, launchConfig);
            }

            (launcher ?? new DefaultPythonLauncher(serviceProvider, launchConfig)).LaunchProject(debug);
        }

        public bool SupportsContext(IWorkspace workspace, string filePath)
        {
            throw new NotImplementedException();
        }
    }
}

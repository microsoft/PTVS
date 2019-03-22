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

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Settings;

namespace Microsoft.PythonTools {
    static class WorkspaceExtensions {
        private const string PythonSettingsType = "PythonSettings";
        private const string InterpreterProperty = "Interpreter";
        private const string SearchPathsProperty = "SearchPaths";

        public static string GetStringProperty(this IWorkspace workspace, string propertyName) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var settingsMgr = workspace.GetSettingsManager();
            var settings = settingsMgr.GetAggregatedSettings(PythonSettingsType);
            settings.GetProperty(propertyName, out string propertyVal);

            return propertyVal;
        }

        public static bool? GetBoolProperty(this IWorkspace workspace, string propertyName) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var settingsMgr = workspace.GetSettingsManager();
            var settings = settingsMgr.GetAggregatedSettings(PythonSettingsType);
            if (settings.GetProperty(propertyName, out bool propertyVal) == WorkspaceSettingsResult.Success) {
                return propertyVal;
            }

            return null;
        }

        public static string GetInterpreter(this IWorkspace workspace) {
            return workspace.GetStringProperty(InterpreterProperty);
        }

        public static string[] GetSearchPaths(this IWorkspace workspace) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var settingsMgr = workspace.GetSettingsManager();
            var settings = settingsMgr.GetAggregatedSettings(PythonSettingsType);
            var searchPaths = settings.UnionPropertyArray<string>(SearchPathsProperty);

            return searchPaths.ToArray();
        }

        public static string[] GetAbsoluteSearchPaths(this IWorkspace workspace) {
            return workspace.GetSearchPaths()
                .Select(sp => PathUtils.GetAbsoluteDirectoryPath(workspace.Location, sp))
                .ToArray();
        }

        public static async Task SetPropertyAsync(this IWorkspace workspace, string propertyName, string propertyVal) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (propertyName == null) {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var settingsMgr = workspace.GetSettingsManager();
            using (var persist = await settingsMgr.GetPersistanceAsync(true)) {
                var writer = await persist.GetWriter(PythonSettingsType);
                if (propertyVal != null) {
                    writer.SetProperty(propertyName, propertyVal);
                } else {
                    writer.Delete(propertyName);
                }
            }
        }

        public static async Task SetPropertyAsync(this IWorkspace workspace, string propertyName, bool? propertyVal) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (propertyName == null) {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var settingsMgr = workspace.GetSettingsManager();
            using (var persist = await settingsMgr.GetPersistanceAsync(true)) {
                var writer = await persist.GetWriter(PythonSettingsType);
                if (propertyVal.HasValue) {
                    writer.SetProperty(propertyName, propertyVal.Value);
                } else {
                    writer.Delete(propertyName);
                }
            }
        }

        public static Task SetInterpreterAsync(this IWorkspace workspace, string interpreter) {
            return workspace.SetPropertyAsync(InterpreterProperty, interpreter);
        }

        public static IPythonInterpreterFactory GetInterpreterFactory(this IWorkspace workspace, IInterpreterRegistryService registryService, IInterpreterOptionsService optionsService) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (registryService == null) {
                throw new ArgumentNullException(nameof(registryService));
            }

            if (optionsService == null) {
                throw new ArgumentNullException(nameof(optionsService));
            }

            var interpreter = workspace.GetInterpreter();

            IPythonInterpreterFactory factory = null;
            if (interpreter != null) {
                factory = registryService.FindInterpreter(interpreter);
                if (factory == null) {
                    if (PathUtils.IsValidPath(interpreter) && !Path.IsPathRooted(interpreter)) {
                        interpreter = workspace.MakeRooted(interpreter);
                    }
                    factory = registryService.Interpreters.SingleOrDefault(f => PathUtils.IsSamePath(f.Configuration.InterpreterPath, interpreter));
                }
            }

            return factory ?? optionsService.DefaultInterpreter;
        }

        public static Task SetInterpreterFactoryAsync(this IWorkspace workspace, IPythonInterpreterFactory factory) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            return workspace.SetInterpreterAsync(factory?.Configuration.Id);
        }

        public static string GetRequirementsTxtPath(this IWorkspace workspace) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var reqsPath = PathUtils.GetAbsoluteFilePath(workspace.Location, "requirements.txt");
            return File.Exists(reqsPath) ? reqsPath : null;
        }

        public static string GetEnvironmentYmlPath(this IWorkspace workspace) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var yamlPath = PathUtils.GetAbsoluteFilePath(workspace.Location, "environment.yml");
            return File.Exists(yamlPath) ? yamlPath : null;
        }

        public static string GetName(this IWorkspace workspace) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            return PathUtils.GetFileOrDirectoryName(workspace.Location);
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Workspace;

namespace Microsoft.PythonTools {
    static class WorkspaceExtensions {
        private const string PythonSettingsType = "PythonSettings";
        private const string InterpreterProperty = "Interpreter";

        public static string GetInterpreter(this IWorkspace workspace) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var settingsMgr = workspace.GetSettingsManager();
            var settings = settingsMgr.GetAggregatedSettings(PythonSettingsType);
            settings.GetProperty(InterpreterProperty, out string interpreter);

            return interpreter;
        }

        public static async Task SetInterpreter(this IWorkspace workspace, string interpreter) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            var settingsMgr = workspace.GetSettingsManager();
            using (var persist = await settingsMgr.GetPersistanceAsync(true)) {
                var writer = await persist.GetWriter(PythonSettingsType);
                if (interpreter != null) {
                    writer.SetProperty(InterpreterProperty, interpreter);
                } else {
                    writer.Delete(InterpreterProperty);
                }
            }
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

        public static Task SetInterpreterFactory(this IWorkspace workspace, IPythonInterpreterFactory factory) {
            if (workspace == null) {
                throw new ArgumentNullException(nameof(workspace));
            }

            return workspace.SetInterpreter(factory.Configuration.Id);
        }
    }
}

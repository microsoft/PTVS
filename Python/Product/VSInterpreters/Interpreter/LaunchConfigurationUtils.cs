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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Interpreter {
    public class LaunchConfigurationUtils {
        public static Dictionary<string, string> GetFullEnvironment(LaunchConfiguration config, IServiceProvider serviceProvider, UIThreadBase uiThread) {
            if (config.Interpreter == null) {
                throw new ArgumentNullException(nameof(Interpreter));
            }
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }


            // Start with global environment, add configured environment,
            // then add search paths.
            var baseEnv = System.Environment.GetEnvironmentVariables();
            // Clear search paths from the global environment. The launch
            // configuration should include the existing value

            var pathVar = config.Interpreter?.PathEnvironmentVariable;
            if (string.IsNullOrEmpty(pathVar)) {
                pathVar = "PYTHONPATH";
            }
            baseEnv[pathVar] = string.Empty;

            if (CondaUtils.IsCondaEnvironment(config.Interpreter.GetPrefixPath())) {
                var condaExe = CondaUtils.GetRootCondaExecutablePath(serviceProvider);
                var prefixPath = config.Interpreter.GetPrefixPath();
                if (File.Exists(condaExe) && Directory.Exists(prefixPath)) {
                    var condaEnv = uiThread.InvokeTaskSync(() => CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(condaExe, prefixPath), CancellationToken.None);
                    baseEnv = PathUtils.MergeEnvironments(baseEnv.AsEnumerable<string, string>(), condaEnv, "Path", pathVar);
                }
            }

            var env = PathUtils.MergeEnvironments(
                baseEnv.AsEnumerable<string, string>(),
                config.GetEnvironmentVariables(),
                "Path", pathVar
            );
            if (config.SearchPaths != null && config.SearchPaths.Any()) {
                env = PathUtils.MergeEnvironments(
                    env,
                    new[] {
                    new KeyValuePair<string, string>(
                        pathVar,
                        PathUtils.JoinPathList(config.SearchPaths)
                    )
                    },
                    pathVar
                );
            }
            return env;
        }
    }
}

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
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Debug;

namespace Microsoft.PythonTools.Workspace {
    [ExportLaunchDebugTarget(
        ProviderType,
        new[] { ".pyproj" }
    )]
    class PyprojLaunchDebugTargetProvider : ILaunchDebugTargetProvider {
        private const string ProviderType = "F2B8B667-3D13-4E51-B067-00C188D0EB7E";

        public const string LaunchTypeName = "pyproj";

        // Set by the workspace, not by our users
        internal const string ProjectKey = "target";

        public const string JsonSchema = @"{
  ""definitions"": {
    ""pyproj"": {
      ""type"": ""object"",
      ""properties"": {
        ""type"": {""type"": ""string"", ""enum"": [ ""pyproj"" ]}
      }
    },
    ""pyprojFile"": {
      ""allOf"": [
        { ""$ref"": ""#/definitions/default"" },
        { ""$ref"": ""#/definitions/pyproj"" }
      ]
    }
  },
    ""defaults"": {
        "".pyproj"": { ""$ref"": ""#/definitions/pyproj"" }
    },
    ""configuration"": ""#/definitions/pyprojFile""
}";

        public void LaunchDebugTarget(IWorkspace workspace, IServiceProvider serviceProvider, DebugLaunchActionContext debugLaunchActionContext) {
            var settings = debugLaunchActionContext.LaunchConfiguration;
            var moniker = settings.GetValue(ProjectKey, string.Empty);
            if (string.IsNullOrEmpty(moniker)) {
                throw new InvalidOperationException();
            }

            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            var debugger = serviceProvider.GetShellDebugger();

            var proj = solution.EnumerateLoadedPythonProjects()
                .FirstOrDefault(p => string.Equals(p.GetMkDocument(), moniker, StringComparison.OrdinalIgnoreCase));

            if (proj == null) {
                throw new InvalidOperationException();
            }

            ErrorHandler.ThrowOnFailure(proj.GetLauncher().LaunchProject(true));
        }

        public bool SupportsContext(IWorkspace workspace, string filePath) {
            throw new NotImplementedException();
        }
    }
}

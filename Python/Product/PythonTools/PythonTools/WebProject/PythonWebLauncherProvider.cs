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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    [Export(typeof(IPythonLauncherProvider))]
    class PythonWebLauncherProvider : IPythonLauncherProvider2 {
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

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

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            var config = project.GetLaunchConfigurationOrThrow();
            
            // Check project type GUID and enable the Django-specific features
            // of the debugger if required.
            var projectGuids = project.GetUnevaluatedProperty("ProjectTypeGuids") ?? "";
            // HACK: Literal GUID string to avoid introducing Django-specific public API
            // We don't want to expose a constant from PythonTools.dll.
            // TODO: Add generic breakpoint extension point
            // to avoid having to pass this property for Django and any future
            // extensions.
            if (projectGuids.IndexOf("5F0BE9CA-D677-4A4D-8806-6076C0FAAD37", StringComparison.OrdinalIgnoreCase) >= 0) {
                config.LaunchOptions["DjangoDebug"] = "true";
            }

            return new PythonWebLauncher(_serviceProvider, config, config, config);
        }
    }
}

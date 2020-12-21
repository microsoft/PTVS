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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    [Export(typeof(IPythonLauncherProvider))]
    class DefaultLauncherProvider : IPythonLauncherProvider {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonToolsService _pyService;
        internal const string DefaultLauncherName = "Standard Python launcher";

        [ImportingConstructor]
        public DefaultLauncherProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new DefaultPythonLauncherOptions(properties);
        }

        public string Name {
            get {
                return DefaultLauncherName;
            }
        }

        public string LocalizedName {
            get {
                return Strings.DefaultLauncherName;
            }
        }

        public string Description {
            get {
                return Strings.DefaultLauncherDescription;
            }
        }

        public int SortPriority {
            get {
                return 0;
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new DefaultPythonLauncher(_serviceProvider, project.GetLaunchConfigurationOrThrow());
        }
    }
}

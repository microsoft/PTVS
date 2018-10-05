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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Environments {
    sealed class AddInstalledEnvironmentView : EnvironmentViewBase {
        private readonly IPythonToolsLogger _logger;

        public AddInstalledEnvironmentView(
            IServiceProvider serviceProvider,
            ProjectView[] projects,
            ProjectView selectedProject
        ) : base(serviceProvider, projects, selectedProject) {
            _logger = Site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
            PageName = Strings.AddInstalledEnvironmentTabHeader;
            AcceptCaption = Strings.AddInstalledEnvironmentLaunch;
            IsAcceptShieldVisible = true;
        }

        public override Task ApplyAsync() {
            _logger?.LogEvent(PythonLogEvent.InstallEnv, null);

            var setupService = Site.GetService(typeof(SVsSetupCompositionService)) as IVsSetupCompositionService;
            string installerPath = setupService?.InstallerPath;
            if (File.Exists(installerPath)) {
                Process.Start(installerPath)?.Dispose();
            }
            return Task.CompletedTask;
        }

        public override string ToString() {
            return Strings.AddInstalledEnvironmentTabHeader;
        }
    }
}

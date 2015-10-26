// Visual Studio Shared Project
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


using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {

    [ComVisible(true)]
    internal class CommonProjectConfig : ProjectConfig {
        private readonly CommonProjectNode/*!*/ _project;

        public CommonProjectConfig(CommonProjectNode/*!*/ project, string configuration)
            : base(project, configuration) {
            _project = project;
        }

        public override int DebugLaunch(uint flags) {
            IProjectLauncher starter = _project.GetLauncher();

            __VSDBGLAUNCHFLAGS launchFlags = (__VSDBGLAUNCHFLAGS)flags;
            if ((launchFlags & __VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug) == __VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug) {
                //Start project with no debugger
                return starter.LaunchProject(false);
            } else {
                //Start project with debugger 
                return starter.LaunchProject(true);
            }
        }
    }
}

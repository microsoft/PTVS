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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop.VSCodeDebuggerHost;

namespace Microsoft.PythonTools.Debugger {
    [ComVisible(true)]
    [Guid("C2990BF1-A87B-4459-9478-322482C535D6")]
    public sealed class DebugAdapterLauncher : IAdapterLauncher {
        public const string DebugAdapterLauncherCLSID = "{C2990BF1-A87B-4459-9478-322482C535D6}";
        public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
        public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

        public DebugAdapterLauncher(){}

        public void Initialize(IDebugAdapterHostContext context) {
        }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            // ITargetHostInterop provides a convenience wrapper to start the process
            // return targetInterop.ExecuteCommandAsync(path, "");

            // If you need more control use the DebugAdapterProcess
            return DebugAdapterProcess.Start(launchInfo.LaunchJson);
        }
        public void UpdateLaunchOptions(IAdapterLaunchInfo launchInfo) {
        }
    }
}

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
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Forms;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Timer = System.Timers.Timer;

namespace Microsoft.PythonTools.Debugger {
    internal sealed class DebugAdapterProcess {
        private readonly ITargetHostInterop _targetInterop;
        private readonly string _debuggerAdapterDirectory;

        private ITargetHostProcess _targetHostProcess;
        private string _webBrowserUrl;

        public DebugAdapterProcess(ITargetHostInterop targetInterop, string debuggerAdapterDirectory) {
            _targetInterop = targetInterop ?? throw new ArgumentNullException(nameof(targetInterop));
            _debuggerAdapterDirectory = debuggerAdapterDirectory ?? throw new ArgumentNullException(nameof(debuggerAdapterDirectory));
        }

        public ITargetHostProcess StartProcess(string pythonExePath, string webBrowserUrl) {
            if (string.IsNullOrEmpty(pythonExePath)) {
                MessageBox.Show(Strings.PythonInterpreterPathNullOrEmpty, Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            _webBrowserUrl = webBrowserUrl;
            _targetHostProcess = _targetInterop.ExecuteCommandAsync(pythonExePath, "\"" + _debuggerAdapterDirectory + "\"");

            if (!string.IsNullOrEmpty(webBrowserUrl) && Uri.TryCreate(webBrowserUrl, UriKind.RelativeOrAbsolute, out _)) {
                Timer timer = new Timer(5000);
                timer.Elapsed += OnBrowserLaunchElapsedTimer;
                timer.AutoReset = false;
                timer.Start();
            }

            return _targetHostProcess;
        }

        private void OnBrowserLaunchElapsedTimer(object sender, ElapsedEventArgs e) {
            Uri.TryCreate(_webBrowserUrl, UriKind.RelativeOrAbsolute, out var uri);
            OnPortOpenedHandler.CreateHandler(uri.Port, null, null, () => _targetHostProcess.HasExited, LaunchBrowserDebugger);
        }

        private void LaunchBrowserDebugger() {
            var vsDebugger = (IVsDebugger2)VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger));

            var info = new VsDebugTargetInfo2();
            var infoSize = Marshal.SizeOf(info);
            info.cbSize = (uint)infoSize;
            info.bstrExe = _webBrowserUrl;
            info.dlo = (uint)_DEBUG_LAUNCH_OPERATION3.DLO_LaunchBrowser;
            info.LaunchFlags = (uint)__VSDBGLAUNCHFLAGS4.DBGLAUNCH_UseDefaultBrowser | (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug;
            IntPtr infoPtr = Marshal.AllocCoTaskMem(infoSize);
            Marshal.StructureToPtr(info, infoPtr, false);

            try {
                vsDebugger.LaunchDebugTargets2(1, infoPtr);
            } finally {
                if (infoPtr != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(infoPtr);
                }
            }
        }
    }
}

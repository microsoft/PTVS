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

using System.ComponentModel;

namespace Microsoft.PythonTools.Debugger.Concord {
    public static class DebuggerOptions {
        // These are intentionally not implemented as auto-properties to enable easily changing them at runtime, including when stopped in native code.
        private static bool _showNativePythonFrames;
        private static bool _usePythonStepping;
        private static bool _showCppViewNodes;
        private static bool _showPythonViewNodes;

        public static event PropertyChangedEventHandler PropertyChanged;

        public static bool ShowNativePythonFrames {
            get {
                return _showNativePythonFrames;
            }
            set {
                _showNativePythonFrames = value;
                RaisePropertyChanged("ShowNativePythonFrames");
            }
        }

        public static bool UsePythonStepping {
            get {
                return _usePythonStepping;
            }
            set {
                _usePythonStepping = value;
                RaisePropertyChanged("UsePythonStepping");
            }
        }

        public static bool ShowCppViewNodes {
            get {
                return _showCppViewNodes;
            }
            set {
                _showCppViewNodes = value;
                RaisePropertyChanged("ShowCppViewNodes");
            }
        }

        public static bool ShowPythonViewNodes {
            get {
                return _showPythonViewNodes;
            }
            set {
                _showPythonViewNodes = value;
                RaisePropertyChanged("ShowPythonViewNodes");
            }
        }

        static DebuggerOptions() {
            ShowNativePythonFrames = false;
            UsePythonStepping = true;
            ShowCppViewNodes = false;
            ShowPythonViewNodes = true;
        }

        private static void RaisePropertyChanged(string propertyName) {
            var propertyChanged = PropertyChanged;
            if (propertyChanged != null) {
                propertyChanged(null, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    /// <summary>
    /// Propagates changes to <see cref="DebuggerOptions"/> from VS to msvsmon.
    /// </summary>
    internal class DebuggerOptionsPropagator : DkmDataItem {
        private readonly DkmProcess _process;
        public DebuggerOptionsPropagator(DkmProcess process) {
            _process = process;
            DebuggerOptions.PropertyChanged += DebuggerOptions_PropertyChanged;
            DebuggerOptions_PropertyChanged(null, null);
        }

        protected override void OnClose() {
            DebuggerOptions.PropertyChanged -= DebuggerOptions_PropertyChanged;
            base.OnClose();
        }

        private void DebuggerOptions_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            new RemoteComponent.SetDebuggerOptions {
                ShowNativePythonFrames = DebuggerOptions.ShowNativePythonFrames,
                UsePythonStepping = DebuggerOptions.UsePythonStepping,
                ShowCppViewNodes = DebuggerOptions.ShowCppViewNodes,
                ShowPythonViewNodes = DebuggerOptions.ShowPythonViewNodes
            }.SendLower(_process);
        }
    }
}

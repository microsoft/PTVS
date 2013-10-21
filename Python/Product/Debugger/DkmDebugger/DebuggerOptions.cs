/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.ComponentModel;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger {
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

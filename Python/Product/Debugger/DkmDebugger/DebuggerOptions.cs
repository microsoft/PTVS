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

namespace Microsoft.PythonTools.DkmDebugger {
    public static class DebuggerOptions {
        // These are intentionally not implemented as auto-properties to enable easily changing them at runtime, including when stopped in native code.
        private static bool _showNativePythonFrames;
        private static bool _usePythonStepping;
        private static bool _showCppViewNodes;
        private static bool _showPythonViewNodes;

        public static bool ShowNativePythonFrames {
            get { return _showNativePythonFrames; }
            set { _showNativePythonFrames = value; }
        }

        public static bool UsePythonStepping {
            get { return _usePythonStepping; }
            set { _usePythonStepping = value; }
        }

        public static bool ShowCppViewNodes {
            get { return _showCppViewNodes; }
            set { _showCppViewNodes = value; }
        }

        public static bool ShowPythonViewNodes {
            get { return _showPythonViewNodes; }
            set { _showPythonViewNodes = value; }
        }

        static DebuggerOptions() {
            ShowNativePythonFrames = false;
            UsePythonStepping = true;
            ShowCppViewNodes = false;
            ShowPythonViewNodes = true;
        }
    }
}

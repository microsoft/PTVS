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

namespace Microsoft.PythonTools.Debugger {
    sealed class OutputEventArgs : EventArgs {
        private readonly string _output;
        private readonly PythonThread _thread;
        private readonly bool _isStdOut;

        public OutputEventArgs(PythonThread thread, string output, bool isStdOut) {
            _thread = thread;
            _output = output;
            _isStdOut = isStdOut;
        }

        public PythonThread Thread {
            get {
                return _thread;
            }
        }

        public string Output {
            get {
                return _output;
            }
        }

        public bool IsStdOut {
            get {
                return _isStdOut;
            }
        }
    }
}

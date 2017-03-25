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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Debugger {
    class PythonThread {
        private readonly long _identity;
        private readonly PythonProcess _process;
        private readonly bool _isWorkerThread;
        private string _name;
        private IList<PythonStackFrame> _frames;

        internal PythonThread(PythonProcess process, long identity, bool isWorkerThread) {
            _process = process;
            _identity = identity;
            _isWorkerThread = isWorkerThread;
            _name = "";
        }

        public Task StepIntoAsync(CancellationToken ct) =>
            _process.SendStepIntoAsync(_identity, ct);

        public Task StepOverAsync(CancellationToken ct) =>
            _process.SendStepOverAsync(_identity, ct);

        public Task StepOutAsync(CancellationToken ct) =>
            _process.SendStepOutAsync(_identity, ct);

        public Task ResumeAsync(CancellationToken ct) =>
            _process.SendResumeThreadAsync(_identity, ct);

        public Task AutoResumeAsync(CancellationToken ct) =>
            _process.AutoResumeThread(_identity, ct);

        internal Task ClearSteppingStateAsync(CancellationToken ct) =>
            _process.SendClearSteppingAsync(_identity, ct);

        public IList<PythonStackFrame> Frames {
            get {
                return _frames;
            }
            set {
                _frames = value;
            }
        }

        public string Name {
            get {
                return _name;
            }
            set {
                _name = value;
            }
        }

        public bool IsWorkerThread => _isWorkerThread;

        public PythonProcess Process => _process;

        internal long Id => _identity;
    }
}

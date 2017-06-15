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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Debugger {
    // Must be in sync with BREAKPOINT_CONDITION_* constants in ptvsd/debugger.py.
    enum PythonBreakpointConditionKind {
        Always = 0,
        WhenTrue = 1,
        WhenChanged = 2
    }

    // Must be in sync with BREAKPOINT_PASS_COUNT_* constants in ptvsd/debugger.py.
    enum PythonBreakpointPassCountKind {
        Always = 0,
        Every = 1,
        WhenEqual = 2,
        WhenEqualOrGreater = 3
    }

    class PythonBreakpoint {
        private readonly PythonProcess _process;
        private readonly string _filename;
        private readonly int _lineNo, _breakpointId;
        private readonly bool _isDjangoBreakpoint;
        private PythonBreakpointConditionKind _conditionKind;
        private string _condition;
        private PythonBreakpointPassCountKind _passCountKind;
        private int _passCount;

        public PythonBreakpoint(
            PythonProcess process,
            string filename,
            int lineNo,
            PythonBreakpointConditionKind conditionKind,
            string condition,
            PythonBreakpointPassCountKind passCountKind,
            int passCount,
            int breakpointId,
            bool isDjangoBreakpoint = false
        ) {
            Debug.Assert(conditionKind != PythonBreakpointConditionKind.Always || string.IsNullOrEmpty(condition));
            Debug.Assert(passCountKind != PythonBreakpointPassCountKind.Always || passCount == 0);

            _process = process;
            _filename = filename;
            _lineNo = lineNo;
            _breakpointId = breakpointId;
            _conditionKind = conditionKind;
            _condition = condition;
            _passCountKind = passCountKind;
            _passCount = passCount;
            _isDjangoBreakpoint = isDjangoBreakpoint;
        }

        /// <summary>
        /// Requests the remote process enable the break point.  An event will be raised on the process
        /// when the break point is received.
        /// </summary>
        public async Task AddAsync(CancellationToken ct) {
            await _process.BindBreakpointAsync(this, ct);
        }

        public bool IsDjangoBreakpoint => _isDjangoBreakpoint;

        /// <summary>
        /// Removes the provided break point
        /// </summary>
        public Task RemoveAsync(CancellationToken ct) {
            return _process.RemoveBreakpointAsync(this, ct);
        }

        public Task DisableAsync(CancellationToken ct) {
            return _process.DisableBreakpointAsync(this, ct);
        }

        internal int Id => _breakpointId;

        public string Filename => _filename;

        public int LineNo => _lineNo;

        public PythonBreakpointConditionKind ConditionKind => _conditionKind;

        public string Condition => _condition;

        public PythonBreakpointPassCountKind PassCountKind => _passCountKind;

        public int PassCount => _passCount;

        internal Task SetConditionAsync(PythonBreakpointConditionKind kind, string condition, CancellationToken ct) {
            _conditionKind = kind;
            _condition = condition;
            return _process.SetBreakpointConditionAsync(this, ct);
        }

        internal Task SetPassCountAsync(PythonBreakpointPassCountKind kind, int passCount, CancellationToken ct) {
            _passCountKind = kind;
            _passCount = passCount;
            return _process.SetBreakpointPassCountAsync(this, ct);
        }

        internal Task<int> GetHitCountAsync(CancellationToken ct = default(CancellationToken)) {
            return _process.GetBreakpointHitCountAsync(this, ct);
        }

        internal Task SetHitCountAsync(int count, CancellationToken ct = default(CancellationToken)) {
            return _process.SetBreakpointHitCountAsync(this, count, ct);
        }
    }
}

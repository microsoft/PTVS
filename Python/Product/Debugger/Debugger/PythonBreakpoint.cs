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

using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Debugger {
    // Must be in sync with BREAKPOINT_CONDITION_* constants in visualstudio_py_debugger.py.
    enum PythonBreakpointConditionKind {
        Always = 0,
        WhenTrue = 1,
        WhenChanged = 2
    }

    // Must be in sync with BREAKPOINT_PASS_COUNT_* constants in visualstudio_py_debugger.py.
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
        public void Add() {
            _process.BindBreakpoint(this);
        }

        public bool IsDjangoBreakpoint {
            get {
                return _isDjangoBreakpoint;
            }
        }

        /// <summary>
        /// Removes the provided break point
        /// </summary>
        public void Remove() {
            _process.RemoveBreakPoint(this);
        }

        public void Disable() {
            _process.DisableBreakPoint(this);
        }

        internal int Id {
            get { return _breakpointId; }
        }

        public string Filename {
            get { return _filename; }
        }

        public int LineNo {
            get { return _lineNo; }
        }

        public PythonBreakpointConditionKind ConditionKind {
            get { return _conditionKind; }
        }

        public string Condition {
            get { return _condition; }
        }

        public PythonBreakpointPassCountKind PassCountKind {
            get { return _passCountKind; }
        }

        public int PassCount {
            get { return _passCount; }
        }

        internal void SetCondition(PythonBreakpointConditionKind kind, string condition) {
            _conditionKind = kind;
            _condition = condition;
            _process.SetBreakPointCondition(this);
        }

        internal void SetPassCount(PythonBreakpointPassCountKind kind, int passCount) {
            _passCountKind = kind;
            _passCount = passCount;
            _process.SetBreakPointPassCount(this);
        }

        internal Task<int> GetHitCountAsync() {
            return _process.GetBreakPointHitCountAsync(this);
        }

        internal void SetHitCount(int count) {
            _process.SetBreakPointHitCount(this, count);
        }
    }
}

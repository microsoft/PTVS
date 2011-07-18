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


namespace Microsoft.PythonTools.Debugger {
    class PythonBreakpoint {
        private readonly PythonProcess _process;
        private readonly string _filename;
        private readonly int _lineNo, _breakpointId;
        private bool _breakWhenChanged;
        private string _condition;

        public PythonBreakpoint(PythonProcess process, string filename, int lineNo, string condition, bool breakWhenChanged, int breakpointId) {
            _process = process;
            _filename = filename;
            _lineNo = lineNo;
            _breakpointId = breakpointId;
            _condition = condition;
            _breakWhenChanged = breakWhenChanged;
        }

        /// <summary>
        /// Requests the remote process enable the break point.  An event will be raised on the process
        /// when the break point is received.
        /// </summary>
        public void Add() {
            _process.BindBreakpoint(this);
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

        public string Filename {
            get {
                return _filename;
            }
        }

        public int LineNo {
            get {
                return _lineNo;
            }
        }

        public string Condition {
            get {
                return _condition;
            }
        }

        internal int Id {
            get {
                return _breakpointId;
            }
        }

        public bool BreakWhenChanged { 
            get { 
                return _breakWhenChanged; 
            } 
        }

        internal void SetCondition(string condition, bool breakWhenChanged) {
            _condition = condition;
            _breakWhenChanged = breakWhenChanged;

            _process.SetBreakPointCondition(this);
        }
    }
}

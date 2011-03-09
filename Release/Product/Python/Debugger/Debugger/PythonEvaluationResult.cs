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

using System;
using System.Threading;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Represents the result of an evaluation of an expression against a given stack frame.
    /// </summary>
    class PythonEvaluationResult {
        private readonly string _expression, _objRepr, _typeName, _exceptionText, _childText;
        private readonly PythonStackFrame _frame;
        private readonly PythonProcess _process;
        private readonly bool _isExpandable, _childIsIndex;

        /// <summary>
        /// Creates a PythonObject for an expression which successfully returned a value.
        /// </summary>
        public PythonEvaluationResult(PythonProcess process, string objRepr, string typeName, string expression, string childText, bool childIsIndex, PythonStackFrame frame, bool isExpandable) {
            _process = process;
            _expression = expression;
            _frame = frame;
            _objRepr = objRepr;
            _typeName = typeName;
            _isExpandable = isExpandable;
            _childText = childText;
            _childIsIndex = childIsIndex;
        }

        /// <summary>
        /// Creates a PythonObject for an expression which raised an exception instead of returning a value.
        /// </summary>
        public PythonEvaluationResult(PythonProcess process, string exceptionText, string expression, PythonStackFrame frame) {
            _process = process;
            _expression = expression;
            _frame = frame;
            _exceptionText = exceptionText;
        }

        /// <summary>
        /// Returns true if this object is expandable.  
        /// </summary>
        public bool IsExpandable {
            get {
                return _isExpandable;
            }
        }

        /// <summary>
        /// Gets the list of children which this object contains.  The children can be either
        /// members (x.foo, x.bar) or they can be indexes (x[0], x[1], etc...).  Calling this
        /// causes the children to be determined by communicating with the debuggee.  These
        /// objects can then later be evaluated.  The names returned here are in the form of
        /// "foo" or "0" so they need additional work to append onto this expression.
        /// 
        /// Returns null if the object is not expandable.
        /// </summary>
        public PythonEvaluationResult[] GetChildren(int timeOut) {
            if (!IsExpandable) {
                return null;
            }

            AutoResetEvent childrenEnumed = new AutoResetEvent(false);
            PythonEvaluationResult[] res = null;

            _process.EnumChildren(Expression, _frame, (children) => {
                res = children;
                childrenEnumed.Set();
            });

            while (!_frame.Thread.Process.HasExited && !childrenEnumed.WaitOne(Math.Min(timeOut, 100))) {
                if (timeOut <= 100) {
                    break;
                }
                timeOut -= 100;
            }

            return res;
        }

        /// <summary>
        /// Gets the string representation of this evaluation or null if an exception was thrown.
        /// </summary>
        public string StringRepr {
            get {
                return _objRepr;
            }
        }

        /// <summary>
        /// Gets the type name of the result of this evaluation or null if an exception was thrown.
        /// </summary>
        public string TypeName {
            get {
                return _typeName;
            }
        }

        /// <summary>
        /// Gets the text of the exception which was thrown when evaluating this expression, or null
        /// if no exception was thrown.
        /// </summary>
        public string ExceptionText {
            get {
                return _exceptionText;
            }
        }

        /// <summary>
        /// Gets the expression which was evaluated to return this object.
        /// </summary>
        public string Expression {
            get {
                if (!String.IsNullOrEmpty(_childText)) {
                    if (_childIsIndex) {
                        return _expression + _childText;
                    } else {
                        return _expression + "." + _childText;
                    }
                }

                return _expression;
            }
        }

        public string ChildText {
            get {
                return _childText;
            }
        }

        /// <summary>
        /// Returns the stack frame in which this expression was evaluated.
        /// </summary>
        public PythonStackFrame Frame {
            get {
                return _frame;
            }
        }

        public PythonProcess Process { get { return _process; } }

        public bool ChildIsIndex { get { return _childIsIndex;  } }
    }
}

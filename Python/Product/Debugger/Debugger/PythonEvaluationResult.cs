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
    enum PythonEvaluationResultReprKind {
        Normal,
        Raw,
        RawLen
    }

    [Flags]
    enum PythonEvaluationResultFlags {
        None = 0,
        Expandable = 1,
        MethodCall = 2,
        SideEffects = 4,
        Raw = 8,
        HasRawRepr = 16,
    }

    /// <summary>
    /// Represents the result of an evaluation of an expression against a given stack frame.
    /// </summary>
    class PythonEvaluationResult {
        private readonly string _objRepr, _hexRepr, _typeName, _expression, _childName, _exceptionText;
        private readonly PythonStackFrame _frame;
        private readonly PythonProcess _process;
        private readonly PythonEvaluationResultFlags _flags;
        private readonly long _length;

        /// <summary>
        /// Creates a PythonObject for an expression which successfully returned a value.
        /// </summary>
        public PythonEvaluationResult(PythonProcess process, string objRepr, string hexRepr, string typeName, long length, string expression, string childName, PythonStackFrame frame, PythonEvaluationResultFlags flags) {
            _process = process;
            _objRepr = objRepr;
            _hexRepr = hexRepr;
            _typeName = typeName;
            _length = length;
            _expression = expression;
            _childName = childName;
            _frame = frame;
            _flags = flags;
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

        public PythonEvaluationResultFlags Flags {
            get { return _flags; }
        }

        /// <summary>
        /// Returns true if this object is expandable.  
        /// </summary>
        public bool IsExpandable {
            get {
                return _flags.HasFlag(PythonEvaluationResultFlags.Expandable);
            }
        }

        /// <summary>
        /// Gets the list of children which this object contains.  The children can be either
        /// members (x.fob, x.oar) or they can be indexes (x[0], x[1], etc...).  Calling this
        /// causes the children to be determined by communicating with the debuggee.  These
        /// objects can then later be evaluated.  The names returned here are in the form of
        /// "fob" or "0" so they need additional work to append onto this expression.
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
        /// Gets the string representation of this evaluation, or <c>null</c> if repr was not requested or the evaluation
        /// failed with an exception.
        /// </summary>
        public string StringRepr {
            get {
                return _objRepr;
            }
        }

        /// <summary>
        /// Gets the string representation of this evaluation in hexadecimal or null if the hex value was not computable.
        /// </summary>
        public string HexRepr {
            get {
                return _hexRepr;
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
        /// Gets the length of the evaluated value as reported by <c>len()</c>, or <c>0</c> if evaluation failed with an exception.
        /// </summary>
        public long Length {
            get { return _length; }
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
                return _expression;
            }
        }

        /// <summary>
        /// If this evaluation result represents a child of another expression (e.g. an object attribute or a collection element),
        /// the short name of that child that uniquely identifies it relative to the parent; for example: "attr", "[123]", "len()". 
        /// If this is not a child of another expression, <c>null</c>.
        /// </summary>
        public string ChildName {
            get {
                return _childName;
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
    }
}

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
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class ReprOptions {
        private int _maxLength = 1000;

        public PythonLanguageVersion LanguageVersion { get; set; }

        public bool Is64Bit { get; set; }

        public bool HexadecimalDisplay { get; set; }

        public int MaxLength {
            get { return _maxLength;  }
            set {
                if (value < 3) {
                    throw new ArgumentException("MaxLength must be at least 3 (to accomodate '...')");
                }
                _maxLength = value;
            }
        }

        public ReprOptions(DkmProcess process) {
            Is64Bit = process.Is64Bit();
            LanguageVersion = process.GetPythonRuntimeInfo().LanguageVersion;
        }

        public ReprOptions(DkmInspectionContext inspectionContext)
            : this(inspectionContext.Thread.Process) {
            HexadecimalDisplay = (inspectionContext.Radix == 16);
        }
    }

    /// <summary>
    /// A builder for Python object reprs. Analogous to <see cref="StringBuilder"/>, but also provides <c>Append</c> methods
    /// for Python objects and literals, and handles <see cref="PyObject"/> arguments in format strings as recursive repr.
    /// </summary>
    internal class ReprBuilder : ICustomFormatter, IFormatProvider {
        public const int MaxJoinedItems = 10;

        private readonly StringBuilder _sb = new StringBuilder();
        private readonly HashSet<ulong> _visitedObjs;
        private bool _maxLengthExceeded = false;
        private int _nestingLevel = 0;

        public ReprOptions Options { get; private set; }

        public ReprBuilder(ReprOptions options) {
            Options = options;
            _visitedObjs = new HashSet<ulong>();
        }
        
        private ReprBuilder(ReprBuilder parent) {
            Options = parent.Options;
            _visitedObjs = parent._visitedObjs;
            _nestingLevel = parent._nestingLevel;
        }

        public bool IsTopLevel {
            get { return _nestingLevel == 0; }
        }

        public override string ToString() {
            CheckLength();
            return _sb.ToString();
        }

        public void Clear() {
            _sb.Clear();
        }

        public ReprBuilder Append(string s) {
            if (CheckLength()) {
                _sb.Append(s);
            }
            return this;
        }

        private ReprBuilder AppendDecOrHex(object n) {
            string format = Options.HexadecimalDisplay ? "0x{0:x}" : "{0}";
            _sb.AppendFormat(CultureInfo.InvariantCulture, format, n);
            return this;
        }

        public ReprBuilder Append(sbyte n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(byte n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(short n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(ushort n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(int n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(uint n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(long n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(ulong n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(BigInteger n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(float n) {
            AppendFormat("{0}", n);
            return this;
        }

        public ReprBuilder Append(double n) {
            AppendFormat("{0}", n);
            return this;
        }

        public ReprBuilder AppendFormat(string format, object arg0) {
            if (CheckLength()) {
                _sb.AppendFormat(this, format, arg0);
            }
            return this;
        }

        public ReprBuilder AppendFormat(string format, object arg0, object arg1) {
            if (CheckLength()) {
                _sb.AppendFormat(this, format, arg0, arg1);
            }
            return this;
        }

        public ReprBuilder AppendFormat(string format, object arg0, object arg1, object arg2) {
            if (CheckLength()) {
                _sb.AppendFormat(this, format, arg0, arg1, arg2);
            }
            return this;
        }

        public ReprBuilder AppendFormat(string format, object arg0, object arg1, object arg2, object arg3) {
            if (CheckLength()) {
                _sb.AppendFormat(this, format, arg0, arg1, arg2, arg3);
            }
            return this;
        }

        /// <summary>
        /// Appends <paramref name="value"/> represented as a Python literal.
        /// </summary>
        /// <remarks>
        /// Supports numeric types, booleans, and ASCII and Unicode strings. For integer types, representation depends on <see cref="HexadecimalDisplay"/>.
        /// </remarks>
        public ReprBuilder AppendLiteral(object value) {
            if (value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong) {
                return AppendDecOrHex(value);
            } else if (value is BigInteger) {
                AppendDecOrHex(value);
                if (Options.LanguageVersion <= PythonLanguageVersion.V27) {
                    Append("L");
                }
                return this;
            }

            var constExpr = new ConstantExpression(value);
            Append(constExpr.GetConstantRepr(Options.LanguageVersion, escape8bitStrings: true));
            return this;
        }

        /// <summary>
        /// Appends a repr of the object passed as an argument. 
        /// </summary>
        /// <remarks>
        /// This method keeps track of nested <see cref="AppendRepr"/> invocations to detect recursion. If asked for a repr of an object
        /// as direct or indirect part of the computation of that object's repr that was triggered earlier, returns <c>"..."</c>.
        /// </remarks>
        public ReprBuilder AppendRepr(PyObject obj) {
            if (CheckLength()) {
                if (obj == null) {
                    return Append("<NULL>");
                }

                if (_visitedObjs.Add(obj.Address)) {
                    ++_nestingLevel;
                    obj.Repr(this);
                    --_nestingLevel;
                    _visitedObjs.Remove(obj.Address);
                } else {
                    Append("...");
                }
            }
            return this;
        }

        /// <summary>
        /// Calls <paramref name="appender"/> for every item in <paramref name="sequence"/>, and appends <paramref name="separator"/> between every two calls.
        /// </summary>
        /// <remarks>
        /// If there are more than <see cref="MaxJoinedItems"/> items in <param name="sequence"/>, <paramref name="appender"/> is only called the first
        /// <see cref="MaxJoinedItems"/> items; after that, another separator is appended, followed by <c>"..."</c>.
        /// </remarks>
        public ReprBuilder AppendJoined<T>(string separator, IEnumerable<T> sequence, Action<T> appender) {
            bool first = true;
            int count = 0;
            foreach (var item in sequence) {
                if (++count > MaxJoinedItems) {
                    Append(separator);
                    Append("...");
                    return this;
                }

                if (first) {
                    first = false;
                } else {
                    Append(separator);
                }

                appender(item);
            }

            return this;
        }

        private bool CheckLength() {
            if (_maxLengthExceeded) {
                return false;
            }

            if (_sb.Length > Options.MaxLength) {
                _sb.Length = Options.MaxLength - 3;
                _sb.Append("...");
                _maxLengthExceeded = true;
                return false;
            }

            return true;
        }

        string ICustomFormatter.Format(string format, object arg, IFormatProvider formatProvider) {
            var obj = arg as PyObject;
            if (obj != null) {
                var builder = new ReprBuilder(Options);
                builder.AppendRepr(obj);
                return builder.ToString();
            } else if (format == "PY") {
                var builder = new ReprBuilder(Options);
                builder.AppendLiteral(arg);
                return builder.ToString();
            } else if (format == "PTR") {
                if (Options.Is64Bit) {
                    return string.Format("0x{0:x16}", arg);
                } else {
                    return string.Format("0x{0:x8}", arg);
                }
            } else {
                var formattable = arg as IFormattable;
                if (formattable != null) {
                    return formattable.ToString(format, formatProvider);
                } else if (arg != null) {
                    return arg.ToString();
                } else {
                    return null;
                }
            }
        }

        object IFormatProvider.GetFormat(Type formatType) {
            if (formatType == typeof(ICustomFormatter)) {
                return this;
            } else {
                return CultureInfo.InvariantCulture.GetFormat(formatType);
            }
        }
    }
}

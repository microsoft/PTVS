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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Common.Parsing.Ast;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord {
    internal class ReprOptions {
        private int _maxLength = 1000;

        public PythonLanguageVersion LanguageVersion { get; set; }

        public bool Is64Bit { get; set; }

        public bool HexadecimalDisplay { get; set; }

        public int MaxLength {
            get { return _maxLength;  }
            set {
                if (value < 3) {
                    throw new ArgumentException(Strings.DebugReprMaxLengthAtLeast3);
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

        private ReprBuilder AppendDecOrHex<T>(T n, T negated = default(T)) where T : IComparable<T> {
            string format;
            if (Options.HexadecimalDisplay) {
                // X format treats everything as unsigned, so we need to take care of the sign ourselves for negative numbers.
                if (n.CompareTo(default(T)) < 0) {
                    _sb.Append('-');
                    n = negated;
                }
                format = "0x{0:x}";
            } else {
                format = "{0}";
            }
            _sb.AppendFormat(CultureInfo.InvariantCulture, format, n);
            return this;
        }

        private static readonly Dictionary<Type, Action<ReprBuilder, object>> appendDecOrHex = new Dictionary<Type, Action<ReprBuilder, object>>() {
            { typeof(sbyte), (rb, x) => rb.AppendDecOrHex((sbyte)x, (sbyte)-(sbyte)x) },
            { typeof(byte), (rb, x) => rb.AppendDecOrHex((byte)x) },
            { typeof(short), (rb, x) => rb.AppendDecOrHex((short)x, (short)-(short)x) },
            { typeof(ushort), (rb, x) => rb.AppendDecOrHex((ushort)x) },
            { typeof(int), (rb, x) => rb.AppendDecOrHex((int)x, (int)-(int)x) },
            { typeof(uint), (rb, x) => rb.AppendDecOrHex((uint)x) },
            { typeof(long), (rb, x) => rb.AppendDecOrHex((long)x, (long)-(long)x) },
            { typeof(ulong), (rb, x) => rb.AppendDecOrHex((ulong)x) },
            { typeof(BigInteger), (rb, x) => rb.AppendDecOrHex((BigInteger)x, -(BigInteger)x) },
        };

        private bool TryAppendDecOrHex(object x) {
            Action<ReprBuilder, object> impl;
            if (appendDecOrHex.TryGetValue(x.GetType(), out impl)) {
                impl(this, x);
                return true;
            } else {
                return false;
            }
        }

        public ReprBuilder Append(sbyte n) {
            return AppendDecOrHex(n, (sbyte)-n);
        }

        public ReprBuilder Append(byte n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(short n) {
            return AppendDecOrHex(n, (short)-n);
        }

        public ReprBuilder Append(ushort n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(int n) {
            return AppendDecOrHex(n, -n);
        }

        public ReprBuilder Append(uint n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(long n) {
            return AppendDecOrHex(n, -n);
        }

        public ReprBuilder Append(ulong n) {
            return AppendDecOrHex(n);
        }

        public ReprBuilder Append(BigInteger n) {
            return AppendDecOrHex(n, -n);
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
            if (TryAppendDecOrHex(value)) {
                if (value is BigInteger && Options.LanguageVersion <= PythonLanguageVersion.V27) {
                    Append("L");
                }
            } else {
                var constExpr = new ConstantExpression(value);
                Append(constExpr.GetConstantRepr(Options.LanguageVersion, escape8bitStrings: true));
            }
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
                    return string.Format("0x{0:X16}", arg);
                } else {
                    return string.Format("0x{0:X8}", arg);
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

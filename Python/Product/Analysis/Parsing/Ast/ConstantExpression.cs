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

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ConstantExpression : Expression {
        private readonly object _value;

        public ConstantExpression(object value) {
            _value = value;
        }

        public object Value {
            get {
                return _value; 
            }
        }

        internal override string CheckAssign() {
            if (_value == null) {
                return "assignment to None";
            }

            return "can't assign to literal";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override string NodeName {
            get {
                return "literal";
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var verbatimPieces = this.GetVerbatimNames(ast);
            var verbatimComments = this.GetListWhiteSpace(ast);
            if (verbatimPieces != null) {
                // string+ / bytes+, such as "abc" "abc", which can spawn multiple lines, and 
                // have comments in between the peices.
                for (int i = 0; i < verbatimPieces.Length; i++) {
                    if (verbatimComments != null && i < verbatimComments.Length) {
                        format.ReflowComment(res, verbatimComments[i]);
                    }
                    res.Append(verbatimPieces[i]);
                }
            } else {
                format.ReflowComment(res, this.GetProceedingWhiteSpaceDefaultNull(ast));
                res.Append(this.GetExtraVerbatimText(ast) ?? GetConstantRepr(ast.LanguageVersion));
            }
        }

        private static bool IsNegativeZero(double value) {
            return (value == 0.0) && double.IsNegativeInfinity(1.0 / value);
        }

        // ToString does not distinguish between negative zero and positive zero, but repr() does, and so should we.
        private static string NegativeZeroAwareToString(double n) {
            return IsNegativeZero(n) ? "-0" : n.ToString("g", nfi);
        }

        private void AppendEscapedString(StringBuilder res, string s, bool escape8bitStrings) {
            res.Append("'");
            foreach (var c in s) {
                switch (c) {
                    case '\n': res.Append("\\n"); break;
                    case '\r': res.Append("\\r"); break;
                    case '\t': res.Append("\\t"); break;
                    case '\'': res.Append("\\'"); break;
                    case '\\': res.Append("\\\\"); break;
                    default:
                        ushort cp = (ushort)c;
                        if (cp > 0xFF) {
                            res.AppendFormat("\\u{0:x04}", cp);
                        } else if (cp < 0x20 || (escape8bitStrings && cp >= 0x7F)) {
                            res.AppendFormat("\\x{0:x02}", cp);
                        } else {
                            res.Append(c);
                        }
                        break;
                }
            }
            res.Append("'");
        }

        public string GetConstantRepr(PythonLanguageVersion version, bool escape8bitStrings = false) {
            if (_value == null) {
                return "None";
            } else if (_value is AsciiString) {
                StringBuilder res = new StringBuilder();
                if (version.Is3x()) {
                    res.Append("b");
                }
                AppendEscapedString(res, ((AsciiString)_value).String, escape8bitStrings);
                return res.ToString();
            } else if (_value is string) {
                StringBuilder res = new StringBuilder();
                if (version.Is2x()) {
                    res.Append("u");
                }
                AppendEscapedString(res, (string)_value, escape8bitStrings);
                return res.ToString();
            } else if (_value is Complex) {
                Complex n = (Complex)_value;
                string real = NegativeZeroAwareToString(n.Real);
                string imag =  NegativeZeroAwareToString(n.Imaginary);
                if (n.Real != 0) {
                    if (!imag.StartsWith("-")) {
                        imag = "+" + imag;
                    }
                    return "(" + real + imag + "j)";
                } else {
                    return imag + "j";
                }
            } else if (_value is BigInteger) {
                if (!version.Is3x()) {
                    return _value.ToString() + "L";
                }
            } else if (_value is double) {
                double n = (double)_value;
                string s = NegativeZeroAwareToString(n);
                // If there's no fractional part, and this is not NaN or +-Inf, G format will not include the decimal
                // point. This is okay if we're using scientific notation as this implies float, but if not, add the
                // decimal point to indicate the type, just like Python repr() does.
                if ((n - Math.Truncate(n)) == 0 && !s.Contains("e")) {
                    s += ".0";
                }
                return s;
            } else if (_value is IFormattable) {
                return ((IFormattable)_value).ToString(null, CultureInfo.InvariantCulture);
            }

            // TODO: We probably need to handle more primitives here
            return _value.ToString();
        }

        private static NumberFormatInfo _nfi;

        private static NumberFormatInfo nfi {
            get {
                if (_nfi == null) {
                    NumberFormatInfo numberFormatInfo = ((CultureInfo)CultureInfo.InvariantCulture.Clone()).NumberFormat;
                    // The CLI formats as "Infinity", but CPython formats differently
                    numberFormatInfo.PositiveInfinitySymbol = "inf";
                    numberFormatInfo.NegativeInfinitySymbol = "-inf";
                    numberFormatInfo.NaNSymbol = "nan";
                    numberFormatInfo.NumberDecimalDigits = 0;
                    _nfi = numberFormatInfo;
                }
                return _nfi;
            }
        }
    }
}

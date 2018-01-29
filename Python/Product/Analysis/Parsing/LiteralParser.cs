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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Summary description for ConstantValue.
    /// </summary>
    internal static class LiteralParser {
        public static string ParseString(string text, bool isRaw, bool isUni) {
            return ParseString(text.ToCharArray(), 0, text.Length, isRaw, isUni, false);
        }

        public static string ParseString(char[] text, int start, int length, bool isRaw, bool isUni, bool normalizeLineEndings) {
            if (text == null) {
                throw new ArgumentNullException("text");
            }

            if (isRaw && !isUni && !normalizeLineEndings) return new String(text, start, length);

            StringBuilder buf = null;
            int i = start;
            int l = start + length;
            int val;
            while (i < l) {
                char ch = text[i++];
                if ((!isRaw || isUni) && ch == '\\') {
                    if (buf == null) {
                        buf = new StringBuilder(length);
                        buf.Append(text, start, i - start - 1);
                    }

                    if (i >= l) {
                        if (isRaw) {
                            buf.Append('\\');
                            break;
                        } else {
                            throw new ArgumentException("Trailing \\ in string");
                        }
                    }
                    ch = text[i++];

                    if (ch == 'u' || ch == 'U') {
                        int len = (ch == 'u') ? 4 : 8;
                        int max = 16;
                        if (isUni && !isRaw) {
                            if (TryParseInt(text, i, len, max, out val)) {
                                buf.Append((char)val);
                                i += len;
                            } else {
                                throw new DecoderFallbackException(@"'unicodeescape' codec can't decode bytes in position {0}: truncated \uXXXX escape".FormatUI(i));
                            }
                        } else {
                            buf.Append('\\');
                            buf.Append(ch);
                        }
                    } else {
                        if (isRaw) {
                            buf.Append('\\');
                            buf.Append(ch);
                            continue;
                        }
                        switch (ch) {
                            case 'a': buf.Append('\a'); continue;
                            case 'b': buf.Append('\b'); continue;
                            case 'f': buf.Append('\f'); continue;
                            case 'n': buf.Append('\n'); continue;
                            case 'r': buf.Append('\r'); continue;
                            case 't': buf.Append('\t'); continue;
                            case 'v': buf.Append('\v'); continue;
                            case '\\': buf.Append('\\'); continue;
                            case '\'': buf.Append('\''); continue;
                            case '\"': buf.Append('\"'); continue;
                            case '\r': if (i < l && text[i] == '\n') i++; continue;
                            case '\n': continue;
                            case 'x': //hex
                                if (!TryParseInt(text, i, 2, 16, out val)) {
                                    goto default;
                                }
                                buf.Append((char)val);
                                i += 2;
                                continue;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7': {
                                    int onechar;
                                    val = ch - '0';
                                    if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                        val = val * 8 + onechar;
                                        i++;
                                        if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                            val = val * 8 + onechar;
                                            i++;
                                        }
                                    }
                                }

                                buf.Append((char)val);
                                continue;
                            default:
                                buf.Append("\\");
                                buf.Append(ch);
                                continue;
                        }
                    }
                } else if (ch == '\r' && normalizeLineEndings) {
                    if (buf == null) {
                        buf = new StringBuilder(length);
                        buf.Append(text, start, i - start - 1);
                    }

                    // normalize line endings
                    if (i < text.Length && text[i] == '\n') {
                        i++;
                    }
                    buf.Append('\n');
                } else if (buf != null) {
                    buf.Append(ch);
                }
            }

            if (buf != null) {
                return buf.ToString();
            }
            return new String(text, start, length);
        }

        internal static List<char> ParseBytes(char[] text, int start, int length, bool isRaw, bool normalizeLineEndings) {
            Debug.Assert(text != null);

            List<char> buf = new List<char>(length);

            int i = start;
            int l = start + length;
            int val;
            while (i < l) {
                char ch = text[i++];
                if (!isRaw && ch == '\\') {
                    if (i >= l) {
                        throw new ArgumentException("Trailing \\ in string");
                    }
                    ch = text[i++];
                    switch (ch) {
                        case 'a': buf.Add('\a'); continue;
                        case 'b': buf.Add('\b'); continue;
                        case 'f': buf.Add('\f'); continue;
                        case 'n': buf.Add('\n'); continue;
                        case 'r': buf.Add('\r'); continue;
                        case 't': buf.Add('\t'); continue;
                        case 'v': buf.Add('\v'); continue;
                        case '\\': buf.Add('\\'); continue;
                        case '\'': buf.Add('\''); continue;
                        case '\"': buf.Add('\"'); continue;
                        case '\r': if (i < l && text[i] == '\n') i++; continue;
                        case '\n': continue;
                        case 'x': //hex
                            if (!TryParseInt(text, i, 2, 16, out val)) {
                                goto default;
                            }
                            buf.Add((char)val);
                            i += 2;
                            continue;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7': {
                                int onechar;
                                val = ch - '0';
                                if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                    val = val * 8 + onechar;
                                    i++;
                                    if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                        val = val * 8 + onechar;
                                        i++;
                                    }
                                }
                            }

                            buf.Add((char)val);
                            continue;
                        default:
                            buf.Add('\\');
                            buf.Add(ch);
                            continue;
                    }
                } else if (ch == '\r' && normalizeLineEndings) {
                    // normalize line endings
                    if (i < text.Length && text[i] == '\n') {
                        i++;
                    }
                    buf.Add('\n');
                } else {
                    buf.Add(ch);
                }
            }

            return buf;
        }

        private static bool HexValue(char ch, out int value) {
            switch (ch) {
                case '0':
                case '\x660': value = 0; break;
                case '1':
                case '\x661': value = 1; break;
                case '2':
                case '\x662': value = 2; break;
                case '3':
                case '\x663': value = 3; break;
                case '4':
                case '\x664': value = 4; break;
                case '5':
                case '\x665': value = 5; break;
                case '6':
                case '\x666': value = 6; break;
                case '7':
                case '\x667': value = 7; break;
                case '8':
                case '\x668': value = 8; break;
                case '9':
                case '\x669': value = 9; break;
                default:
                    if (ch >= 'a' && ch <= 'z') {
                        value = ch - 'a' + 10;
                    } else if (ch >= 'A' && ch <= 'Z') {
                        value = ch - 'A' + 10;
                    } else {
                        value = -1;
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static int HexValue(char ch) {
            int value;
            if (!HexValue(ch, out value)) {
                throw new ArgumentException("bad char for integer value: " + ch);
            }
            return value;
        }

        private static int CharValue(char ch, int b) {
            int val = HexValue(ch);
            if (val >= b) {
                throw new ArgumentException("bad char for the integer value: '{0}' (base {1})".FormatUI(ch, b));
            }
            return val;
        }

        private static bool ParseInt(string text, int b, out int ret) {
            ret = 0;
            long m = 1;
            for (int i = text.Length - 1; i >= 0; i--) {
                // avoid the exception here.  Not only is throwing it expensive,
                // but loading the resources for it is also expensive 
                char c = text[i];
                if (c == '_') {
                    continue;
                }

                long lret = (long)ret + m * CharValue(c, b);
                if (Int32.MinValue <= lret && lret <= Int32.MaxValue) {
                    ret = (int)lret;
                } else {
                    return false;
                }

                m *= b;
                if (Int32.MinValue > m || m > Int32.MaxValue) {
                    return false;
                }
            }
            return true;
        }

        private static bool TryParseInt(char[] text, int start, int length, int b, out int value) {
            value = 0;
            if (start + length > text.Length) {
                return false;
            }
            for (int i = start, end = start + length; i < end; i++) {
                int onechar;
                char c = text[i];
                if (c == '_') {
                    continue;
                } else if (HexValue(c, out onechar) && onechar < b) {
                    value = value * b + onechar;
                } else {
                    return false;
                }
            }
            return true;
        }

        public static object ParseInteger(string text, int b) {
            Debug.Assert(b != 0);
            int iret;
            if (!ParseInt(text, b, out iret)) {
                BigInteger ret = ParseBigInteger(text, b);
                if (ret >= Int32.MinValue && ret <= Int32.MaxValue) {
                    return (int)ret;
                }
                return ret;
            }
            return iret;
        }

        public static object ParseIntegerSign(string text, int b) {
            int start = 0, end = text.Length, saveb = b;
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ArgumentException("base must be >= 2 and <= 36");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            int ret = 0;
            try {
                int saveStart = start;
                for (; ; ) {
                    int digit;
                    if (start >= end) {
                        if (saveStart == start) {
                            throw new ArgumentException("Invalid integer literal");
                        }
                        break;
                    }
                    char c = text[start];
                    if (c != '_') {
                        if (!HexValue(c, out digit)) break;
                        if (!(digit < b)) {
                            if (c == 'l' || c == 'L') {
                                break;
                            }
                            throw new ArgumentException("Invalid integer literal");
                        }

                        checked {
                            // include sign here so that System.Int32.MinValue won't overflow
                            ret = ret * b + sign * digit;
                        }
                    }
                    start++;
                }
            } catch (OverflowException) {
                return ParseBigIntegerSign(text, saveb);
            }

            ParseIntegerEnd(text, start, end);

            return ret;
        }

        private static void ParseIntegerStart(string text, ref int b, ref int start, int end, ref short sign) {
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;
            //  Sign?
            if (start < end) {
                switch (text[start]) {
                    case '-':
                        sign = -1;
                        goto case '+';
                    case '+':
                        start++;
                        break;
                }
            }
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;

            //  Determine base
            if (b == 0) {
                if (start < end && text[start] == '0') {
                    // Hex, oct, or bin
                    if (++start < end) {
                        switch (text[start]) {
                            case 'x':
                            case 'X':
                                start++;
                                b = 16;
                                break;
                            case 'o':
                            case 'O':
                                b = 8;
                                start++;
                                break;
                            case 'b':
                            case 'B':
                                start++;
                                b = 2;
                                break;
                        }
                    }

                    if (b == 0) {
                        // Keep the leading zero
                        start--;
                        b = 8;
                    }
                } else {
                    b = 10;
                }
            }
        }

        private static void ParseIntegerEnd(string text, int start, int end) {
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;

            if (start < end) {
                throw new ArgumentException("invalid integer number literal");
            }
        }

        public static BigInteger ParseBigInteger(string text, int b) {
            Debug.Assert(b != 0);
            BigInteger ret = BigInteger.Zero;
            BigInteger m = BigInteger.One;

            if (text.Length != 0) {

                int i = text.Length - 1;
                if (text[i] == 'l' || text[i] == 'L') i -= 1;

                int groupMax = 7;
                if (b <= 10) groupMax = 9;// 2 147 483 647

                while (i >= 0) {
                    // extract digits in a batch
                    int smallMultiplier = 1;
                    uint uval = 0;

                    for (int j = 0; j < groupMax && i >= 0; j++) {
                        char c = text[i--];
                        if (c != '_') {
                            uval = (uint)(CharValue(c, b) * smallMultiplier + uval);
                            smallMultiplier *= b;
                        }
                    }

                    // this is more generous than needed
                    ret += m * (BigInteger)uval;
                    if (i >= 0) m = m * (smallMultiplier);
                }
            }

            return ret;
        }

        public static BigInteger ParseBigIntegerSign(string text, int b) {
            int start = 0, end = text.Length;
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ArgumentException("base must be >= 2 and <= 36");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            BigInteger ret = BigInteger.Zero;
            int saveStart = start;
            for (; ; ) {
                int digit;
                if (start >= end) {
                    if (start == saveStart) {
                        throw new ArgumentException("Invalid integer literal");
                    }
                    break;
                }
                char c = text[start];
                if (c != '_') {
                    if (!HexValue(c, out digit)) break;
                    if (!(digit < b)) {
                        if (c == 'l' || c == 'L') {
                            break;
                        }
                        throw new ArgumentException("Invalid integer literal");
                    }
                    ret = ret * b + digit;
                }
                start++;
            }

            if (start < end && (text[start] == 'l' || text[start] == 'L')) {
                start++;
            }

            ParseIntegerEnd(text, start, end);

            return sign < 0 ? -ret : ret;
        }


        public static double ParseFloat(string text) {
            try {
                //
                // Strings that end with '\0' is the specific case that CLR libraries allow,
                // however Python doesn't. Since we use CLR floating point number parser,
                // we must check explicitly for the strings that end with '\0'
                //
                if (text != null && text.Length > 0 && text[text.Length - 1] == '\0') {
                    throw new ArgumentException("null byte in float literal");
                }
                return ParseFloatNoCatch(text);
            } catch (OverflowException) {
                return text.TrimStart().StartsWithOrdinal("-") ? Double.NegativeInfinity : Double.PositiveInfinity;
            }
        }

        private static double ParseFloatNoCatch(string text) {
            string s = ReplaceUnicodeDigits(text);
            switch (s.ToLowerInvariant().TrimStart()) {
                case "nan":
                case "+nan":
                case "-nan":
                    return double.NaN;
                case "inf":
                case "+inf":
                    return double.PositiveInfinity;
                case "-inf":
                    return double.NegativeInfinity;
                default:
                    // pass NumberStyles to disallow ,'s in float strings.
                    double res = double.Parse(s.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture);
                    return (res == 0.0 && text.TrimStart().StartsWithOrdinal("-")) ? NegativeZero : res;
            }
        }

        internal const double NegativeZero = -0.0;

        private static string ReplaceUnicodeDigits(string text) {
            StringBuilder replacement = null;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] >= '\x660' && text[i] <= '\x669') {
                    if (replacement == null) replacement = new StringBuilder(text);
                    replacement[i] = (char)(text[i] - '\x660' + '0');
                }
            }
            if (replacement != null) {
                text = replacement.ToString();
            }
            return text;
        }

        // ParseComplex helpers
        private static char[] signs = new char[] { '+', '-' };
        private static Exception ExnMalformed() {
            return new ArgumentException("complex() arg is a malformed string");
        }

        public static Complex ParseImaginary(string text) {
            try {
                return new Complex(0.0, double.Parse(
                    text.Substring(0, text.Length - 1).Replace("_", ""),
                    CultureInfo.InvariantCulture.NumberFormat
                ));
            } catch (OverflowException) {
                return new Complex(0, Double.PositiveInfinity);
            }
        }
    }
}

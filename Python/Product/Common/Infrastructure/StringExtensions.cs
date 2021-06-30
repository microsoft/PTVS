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
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.PythonTools.Infrastructure {
    static class StringExtensions {
#if DEBUG
        private static readonly Regex SubstitutionRegex = new Regex(
            @"\{(\d+)",
            RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(1)
        );

        private static void ValidateFormatString(string str, int argCount) {
            foreach (Match m in SubstitutionRegex.Matches(str)) {
                int index = int.Parse(m.Groups[1].Value);
                if (index >= argCount) {
                    Debug.Fail(string.Format("Format string expects more than {0} args.\n\n{1}", argCount, str));
                }
            }
        }
#else
        [Conditional("DEBUG")]
        private static void ValidateFormatString(string str, int argCount) { }
#endif

        public static string FormatUI(this string str, object arg0) {
            ValidateFormatString(str, 1);
            return string.Format(CultureInfo.CurrentCulture, str, arg0);
        }

        public static string FormatUI(this string str, object arg0, object arg1) {
            ValidateFormatString(str, 2);
            return string.Format(CultureInfo.CurrentCulture, str, arg0, arg1);
        }

        public static string FormatUI(this string str, params object[] args) {
            ValidateFormatString(str, args.Length);
            return string.Format(CultureInfo.CurrentCulture, str, args);
        }

        public static string FormatInvariant(this string str, object arg0) {
            ValidateFormatString(str, 1);
            return string.Format(CultureInfo.InvariantCulture, str, arg0);
        }

        public static string FormatInvariant(this string str, object arg0, object arg1) {
            ValidateFormatString(str, 2);
            return string.Format(CultureInfo.InvariantCulture, str, arg0, arg1);
        }

        public static string FormatInvariant(this string str, params object[] args) {
            ValidateFormatString(str, args.Length);
            return string.Format(CultureInfo.InvariantCulture, str, args);
        }

        public static string IfNullOrEmpty(this string str, string fallback) {
            return string.IsNullOrEmpty(str) ? fallback : str;
        }

        public static bool IsTrue(this string str) {
            bool asBool;
            return !string.IsNullOrWhiteSpace(str) && (
                str.Equals("1", StringComparison.Ordinal) ||
                str.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                (bool.TryParse(str, out asBool) && asBool)
            );
        }

        public static string TrimEndNewline(this string str) {
            if (string.IsNullOrEmpty(str)) {
                return string.Empty;
            }

            if (str[str.Length - 1] == '\n') {
                if (str.Length >= 2 && str[str.Length - 2] == '\r') {
                    return str.Remove(str.Length - 2);
                }
                return str.Remove(str.Length - 1);
            }
            return str;
        }

        public static string Truncate(this string str, int length) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            if (str.Length < length) {
                return str;
            }

            return str.Substring(0, length);
        }

        public static bool StartsWithOrdinal(this string s, string prefix, bool ignoreCase = false) {
            return s?.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;
        }

        public static bool EndsWithOrdinal(this string s, string suffix, bool ignoreCase = false) {
            return s?.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;
        }

        public static int IndexOfOrdinal(this string s, string value, int startIndex = 0, bool ignoreCase = false) {
            return s?.IndexOf(value, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? -1;
        }

        public static int IndexOfOrdinal(this string s, string value, int startIndex, int count, bool ignoreCase = false) {
            return s?.IndexOf(value, startIndex, count, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? -1;
        }
    }
}

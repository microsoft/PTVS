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
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    static class StringExtensions {
#if DEBUG
        private static readonly Regex SubstitutionRegex = new Regex(
            @"\{(\d+)",
            RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(1)
        );

        private static void ValidateFormatString(string str, int argCount) {
            foreach (Match m in SubstitutionRegex.Matches(str)) {
                int index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (index >= argCount) {
                    Debug.Fail(string.Format(CultureInfo.InvariantCulture, "Format string expects more than {0} args.\n\n{1}", argCount, str));
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

        public static bool IsTrue(this string str) {
            bool asBool;
            return !string.IsNullOrWhiteSpace(str) && (
                str.Equals("1", StringComparison.Ordinal) ||
                str.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                (bool.TryParse(str, out asBool) && asBool)
            );
        }

        public static string AsQuotedArguments(this IEnumerable<string> args) {
            return string.Join(" ", args.Select(QuoteArgument).Where(a => !string.IsNullOrEmpty(a)));
        }

        public static string QuoteArgument(this string arg) {
            if (arg == null) {
                // Null arguments are excluded
                return null;
            }

            if (string.IsNullOrEmpty(arg)) {
                // Empty string means empty argument
                return "\"\"";
            }

            if (arg.Length == 1) {
                // Single character never needs quoting
                return arg;
            }

            if (arg.First() == '"' && arg.Last() == '"') {
                // Already quoted
                return arg;
            }

            if (arg.IndexOfAny(new[] { ' ', '"' }) < 0) {
                // Does not need quoting
                return arg;
            }

            if (arg.Last() == '\\') {
                // Need to escape the trailing backslash
                arg += '\\';
            }

            return "\"{0}\"".FormatInvariant(arg);
        }

        public static bool StartsWithOrdinal(this string s, string prefix, bool ignoreCase = false) {
            return s?.StartsWith(prefix, ignoreCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static bool EndsWithOrdinal(this string s, string suffix, bool ignoreCase = false) {
            return s?.StartsWith(suffix, ignoreCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }
}

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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public static class StringExtensions {
        private static readonly bool IgnoreCaseInPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static StringComparer PathsStringComparer { get; } = IgnoreCaseInPaths ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        public static StringComparison PathsStringComparison { get; } = IgnoreCaseInPaths ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

#if DEBUG
        private static readonly Regex SubstitutionRegex = new Regex(
            @"\{(\d+)",
            RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(1)
        );

        private static void ValidateFormatString(string str, int argCount) {
            foreach (Match m in SubstitutionRegex.Matches(str)) {
                var index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
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

        public static int IndexOfEnd(this string s, string substring, StringComparison comparisonType = StringComparison.Ordinal) {
            var i = s.IndexOf(substring, comparisonType);
            return i < 0 ? i : i + substring.Length;
        }

        public static bool IsTrue(this string str) {
            return !string.IsNullOrWhiteSpace(str) && (
                str.Equals("1", StringComparison.Ordinal) ||
                str.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                (bool.TryParse(str, out var asBool) && asBool)
            );
        }

        public static string AsQuotedArguments(this IEnumerable<string> args)
            => string.Join(" ", args.Select(QuoteArgument).Where(a => !string.IsNullOrEmpty(a)));

        private static IEnumerable<int> FindUnescapedChar(string s, char c, int start = 0, int end = int.MaxValue) {
            start -= 1;
            while ((start = s.IndexOf(c, start + 1)) > 0 && start < end) {
                if (s[start - 1] != '\\') {
                    yield return start;
                }
            }
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
                if (!FindUnescapedChar(arg, '"', 1, arg.Length).Any()) {
                    // Already quoted correctly
                    return arg;
                }
                // Needs re-quoting, so strip the existing quotes
                // We do not want to return unquoted though
                arg = arg.Substring(1, arg.Length - 2);
            } else if (arg.IndexOf(' ') < 0 && !FindUnescapedChar(arg, '"').Any()) {
                // Does not need quoting
                return arg;
            }

            if (arg.Length > 1 && arg[arg.Length - 1] == '\\' && arg[arg.Length - 2] != '\\') {
                // Need to escape the trailing backslash
                arg += '\\';
            }

            foreach (var i in FindUnescapedChar(arg, '"').Reverse().ToArray()) {
                // We are going to quote with double quotes, so escape any
                // inline double quotes first
                arg = arg.Insert(i, "\\");
            }

            return "\"{0}\"".FormatInvariant(arg);
        }

        public static bool StartsWithOrdinal(this string s, string prefix, bool ignoreCase = false)
            => s?.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;

        public static bool EndsWithOrdinal(this string s, string suffix, bool ignoreCase = false)
            => s?.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;

        public static bool PathEndsWithAny(this string s, params string[] values)
            => s.EndsWithAnyOrdinal(values, IgnoreCaseInPaths);

        public static bool EndsWithAnyOrdinal(this string s, params string[] values)
            => s.EndsWithAnyOrdinal(values, false);

        public static bool EndsWithAnyOrdinalIgnoreCase(this string s, params string[] values)
            => s.EndsWithAnyOrdinal(values, true);

        public static bool EndsWithAnyOrdinal(this string s, string[] values, bool ignoreCase) {
            if (s == null) {
                return false;
            }

            foreach (var value in values) {
                if (s.EndsWith(value, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        public static bool CharsAreLatin1LetterOrDigitOrUnderscore(this string s, int startIndex, int length) {
            if (s == null) {
                return false;
            }

            for (var i = startIndex; i < startIndex + length; i++) {
                if (!s[i].IsLatin1LetterOrDigitOrUnderscore()) {
                    return false;
                }
            }

            return true;
        }

        public static int IndexOfOrdinal(this string s, string value, int startIndex = 0, bool ignoreCase = false)
            => s?.IndexOf(value, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? -1;

        public static bool EqualsIgnoreCase(this string s, string other)
            => string.Equals(s, other, StringComparison.OrdinalIgnoreCase);

        public static bool EqualsOrdinal(this string s, string other)
            => string.Equals(s, other, StringComparison.Ordinal);

        public static bool PathEquals(this string s, string other)
            => string.Equals(s, other, PathsStringComparison);

        public static int PathCompare(this string s, string other)
            => string.Compare(s, other, PathsStringComparison);

        public static bool EqualsOrdinal(this string s, int index, string other, int otherIndex, int length, bool ignoreCase = false)
            => string.Compare(s, index, other, otherIndex, length, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;
        public static bool ContainsOrdinal(this string s, string value, bool ignoreCase = false)
            => s.IndexOfOrdinal(value, ignoreCase: ignoreCase) != -1;

        public static int GetPathHashCode(this string s)
            => IgnoreCaseInPaths ? StringComparer.OrdinalIgnoreCase.GetHashCode(s) : StringComparer.Ordinal.GetHashCode(s);

        public static string[] Split(this string s, char separator, int startIndex, int length) {
            var count = 0;
            var endIndex = startIndex + length;
            var previousIndex = startIndex;
            var index = s.IndexOf(separator, startIndex);
            while (index != -1 && index < endIndex) {
                if (index > previousIndex) {
                    count++;
                }
                previousIndex = index + 1;
                index = s.IndexOf(separator, previousIndex);
            }

            if (endIndex > previousIndex) {
                count++;
            }

            if (count == 0) {
                return Array.Empty<string>();
            }

            var result = new string[count];

            count = 0;
            previousIndex = startIndex;
            index = s.IndexOf(separator, startIndex);
            while (index != -1 && index < endIndex) {
                if (index > previousIndex) {
                    result[count] = s.Substring(previousIndex, index - previousIndex);
                    count++;
                }
                previousIndex = index + 1;
                index = s.IndexOf(separator, previousIndex);
            }

            if (count < result.Length) {
                result[count] = s.Substring(previousIndex, endIndex - previousIndex);
            }

            return result;
        }

        public static StringSpan Slice(this string s, int start, int length)
            => new StringSpan(s, start, length);

        public static StringSpanSplitSequence SplitIntoSpans(this string s, char separator)
            => s.SplitIntoSpans(separator, 0, s.Length);

        public static StringSpanSplitSequence SplitIntoSpans(this string s, char separator, int start, int length)
            => new StringSpanSplitSequence(s, start, length, separator);

        public static bool TryGetNextNonEmptySpan(this string s, char separator, ref (int start, int length) span)
            => s.TryGetNextNonEmptySpan(separator, s.Length, ref span);

        public static bool TryGetNextNonEmptySpan(this string s, char separator, int substringLength, ref (int start, int length) span) {
            var start = span.start + span.length;
            if (start < 0) {
                return false;
            }

            while (start < substringLength && s[start] == separator) {
                start++;
            }

            if (start == substringLength) {
                span = (-1, 0);
                return false;
            }

            var nextSeparatorIndex = s.IndexOf(separator, start);
            span = (start, nextSeparatorIndex == -1 || nextSeparatorIndex >= substringLength ? substringLength - start : nextSeparatorIndex - start);
            return true;
        }

        public static string[] SplitLines(this string s, params string[] lineEndings) {
            if (lineEndings == null || lineEndings.Length == 0) {
                lineEndings = new[] { "\r\n", "\r", "\n" };
            }

            return s.Split(lineEndings, StringSplitOptions.None);
        }

        public static string NormalizeLineEndings(this string s, string lineEnding = null) {
            if (s == null) {
                return null;
            }

            lineEnding = lineEnding ?? Environment.NewLine;
            return string.Join(lineEnding, s.SplitLines());
        }

        [DebuggerStepThrough]
        public static int GetStableHash(this string s) {
            unchecked {
                var hash = 23;
                foreach (var c in s) {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        /// <summary>
        /// return string representation of hash of the input string.
        /// 
        /// the string representation of the hash is in the form where it can be used in a file system.
        /// </summary>
        public static string GetHashString(this string input) {
            // File name depends on the content so we can distinguish between different versions.
            using (var hash = SHA256.Create()) {
                return Convert
                    .ToBase64String(hash.ComputeHash(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input)))
                    .Replace('/', '_').Replace('+', '-');
            }
        }
    }
}

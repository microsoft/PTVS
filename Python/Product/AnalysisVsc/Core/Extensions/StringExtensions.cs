// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DsTools.Core {
    public static class StringExtensions {
        public static bool EqualsOrdinal(this string s, string other) => string.Equals(s, other, StringComparison.Ordinal);
        public static bool EqualsIgnoreCase(this string s, string other) => string.Equals(s, other, StringComparison.OrdinalIgnoreCase);
        public static bool StartsWithIgnoreCase(this string s, string prefix) => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        public static bool StartsWithOrdinal(this string s, string prefix) => s.StartsWith(prefix, StringComparison.Ordinal);
        public static bool EndsWithIgnoreCase(this string s, string suffix) => s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        public static bool EndsWithOrdinal(this string s, string suffix) => s.EndsWith(suffix, StringComparison.Ordinal);
        public static bool EndsWith(this string s, char ch) => s.Length > 0 && s[s.Length - 1] == ch;
        public static int IndexOfIgnoreCase(this string s, string searchFor) => s.IndexOf(searchFor, StringComparison.OrdinalIgnoreCase);
        public static int IndexOfIgnoreCase(this string s, string searchFor, int startIndex) => s.IndexOf(searchFor, startIndex, StringComparison.OrdinalIgnoreCase);
        public static int IndexOfOrdinal(this string s, string searchFor) => s.IndexOf(searchFor, StringComparison.Ordinal);
        public static int LastIndexOfIgnoreCase(this string s, string searchFor) => s.LastIndexOf(searchFor, StringComparison.OrdinalIgnoreCase);
        public static int LastIndexOfIgnoreCase(this string s, string searchFor, int startIndex) => s.LastIndexOf(searchFor, startIndex, StringComparison.OrdinalIgnoreCase);
        public static bool ContainsIgnoreCase(this string s, string prefix) => s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0;

        public static IEnumerable<int> AllIndexesOfIgnoreCase(this string s, string value, int startIndex = 0, bool allowOverlap = false) {
            var i = startIndex;
            while (i < s.Length) {
                i = s.IndexOf(value, i, StringComparison.OrdinalIgnoreCase);
                if (i < 0) {
                    break;
                }

                yield return i;
                i = allowOverlap ? i + value.Length : i + 1;
            }
        }

        public static bool IsStartOfNewLine(this string s, int index, bool ignoreWhitespaces = false) {
            if (index < 0 || index >= s.Length) {
                return false;
            }

            if (s[index].IsLineBreak()) {
                return false;
            }

            while (index > 0) {
                index--;
                var ch = s[index];
                if (ch.IsLineBreak()) {
                    return true;
                }

                if (!ignoreWhitespaces || !char.IsWhiteSpace(ch)) {
                    return false;
                }
            }

            return true;
        }

        public static string TrimQuotes(this string s) {
            if (s.Length > 0) {
                char quote = s[0];
                if (quote == '\'' || quote == '\"') {
                    if (s.Length > 1 && s[s.Length - 1] == quote) {
                        return s.Substring(1, s.Length - 2);
                    }
                    return s.Substring(1);
                }
            }
            return s;
        }

        public static string Replace(this string s, string oldValue, string newValue, int start, int length) {
            if (string.IsNullOrEmpty(oldValue)) {
                throw new ArgumentException("oldValue can't be null or empty string", nameof(oldValue));
            }

            if (string.IsNullOrEmpty(s)) {
                return s;
            }

            if (start < 0) {
                start = 0;
            }

            if (length < 0) {
                length = 0;
            }

            return new StringBuilder(s)
                .Replace(oldValue, newValue, start, length)
                .ToString();
        }

        public static string RemoveWhiteSpaceLines(this string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            var sb = new StringBuilder(text);
            var lineBreakIndex = sb.Length;
            var isWhiteSpaceOnly = true;
            for (var i = sb.Length - 1; i >= 0; i--) {
                var ch = sb[i];
                if (ch == '\r' || ch == '\n') {
                    if (ch == '\n' && i > 0 && sb[i - 1] == '\r') {
                        i--;
                    }

                    if (isWhiteSpaceOnly) {
                        sb.Remove(i, lineBreakIndex - i);
                    } else if (i == 0) {
                        var rn = sb.Length > 1 && sb[0] == '\r' && sb[1] == '\n';
                        sb.Remove(0, rn ? 2 : 1);
                        break;
                    }

                    lineBreakIndex = i;
                    isWhiteSpaceOnly = true;
                }

                isWhiteSpaceOnly = isWhiteSpaceOnly && char.IsWhiteSpace(ch);
            }

            return sb.ToString();
        }

        public static int SubstringToHex(this string s, int position, int count) {
            int mul = 1 << (4 * (count - 1));
            int result = 0;

            for (int i = 0; i < count; i++) {
                char ch = s[position + i];
                int z;
                if (ch >= '0' && ch <= '9') {
                    z = ch - '0';
                } else if (ch >= 'a' && ch <= 'f') {
                    z = ch - 'a' + 10;
                } else if (ch >= 'A' && ch <= 'F') {
                    z = ch - 'A' + 10;
                } else {
                    return -1;
                }

                result += z * mul;
                mul >>= 4;
            }
            return result;
        }
        /// <summary>
        /// Given a string (typically text from a file) determines
        /// which line break sequence should be used when editing or
        /// formatting the file. If no line breaks found, LF is returned.
        /// </summary>
        public static string GetDefaultLineBreakSequence(this string s) {
            int i = s.IndexOfAny(CharExtensions.LineBreakChars);
            if (i >= 0) {
                if (s[i] == '\n') {
                    if (i + 1 < s.Length && s[i + 1] == '\r') {
                        return "\n\r";
                    }
                    return "\n";
                }
                if (s[i] == '\r') {
                    if (i + 1 < s.Length && s[i + 1] == '\n') {
                        return "\r\n";
                    }
                    return "\r";
                }
            }
            return "\n"; // default
        }

        public static string GetSHA512Hash(this string input) => GetHash(input, SHA512.Create());

        public static string GetSHA256Hash(this string input) => GetHash(input, SHA256.Create());

        private static string GetHash(string input, HashAlgorithm hashAlgorithm) {
            var inputBytes = Encoding.Unicode.GetBytes(input);
            var hash = hashAlgorithm.ComputeHash(inputBytes);
            return BitConverter.ToString(hash);
        }

        public static string GetSHA512FileSystemSafeHash(this string input) => GetFileSystemSafeHash(input, SHA512.Create());

        public static string GetSHA256FileSystemSafeHash(this string input) => GetFileSystemSafeHash(input, SHA256.Create());

        private static string GetFileSystemSafeHash(string input, HashAlgorithm hashAlgorithm) {
            byte[] inputBytes = Encoding.Unicode.GetBytes(input);
            byte[] hashBytes = hashAlgorithm.ComputeHash(inputBytes);

            var hashCharsLength = (int)(hashBytes.Length * 4.0d / 3.0d);
            if (hashCharsLength % 4 != 0) {
                hashCharsLength += 4 - hashCharsLength % 4;
            }

            var hashChars = new char[hashCharsLength];

            Convert.ToBase64CharArray(hashBytes, 0, hashBytes.Length, hashChars, 0);
            return new StringBuilder()
                .Append(hashChars)
                .Replace('+', '-')
                .Replace('/', '.')
                .Replace('=', '_')
                .ToString();
        }

        public static string FormatInvariant(this string format, object arg) =>
            string.Format(CultureInfo.InvariantCulture, format, arg);

        public static string FormatInvariant(this string format, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, format, args);

        public static string FormatCurrent(this string format, object arg) =>
            string.Format(CultureInfo.CurrentCulture, format, arg);

        public static string FormatCurrent(this string format, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, format, args);

        public static long ToLongOrDefault(this string value)
            => long.TryParse(value, out long ret) ? ret : 0;

        public static DateTime ToDateTimeOrDefault(this string value)
            => DateTime.TryParse(value, out DateTime ret) ? ret : default(DateTime);

        public static Guid ToGuid(this string value) {
            using (var md5 = MD5.Create()) {
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }

        public static string RemoveLineBreaks(this string s) 
            => s.Replace('\n', ' ').Replace('\r', ' ').Replace("  ", " ");
    }
}

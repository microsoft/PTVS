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
using System.Linq;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    static class StringExtensions {
        public static bool IsTrue(this string str) {
            bool asBool;
            return !string.IsNullOrWhiteSpace(str) && (
                str.Equals("1") ||
                str.Equals("yes", StringComparison.InvariantCultureIgnoreCase) ||
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

            return $"\"{arg}\"";
        }
    }
}

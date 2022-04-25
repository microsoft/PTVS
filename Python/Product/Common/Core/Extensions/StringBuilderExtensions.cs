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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public static class StringBuilderExtensions {
        public static StringBuilder TrimEnd(this StringBuilder sb) {
            while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1])) {
                sb.Length -= 1;
            }
            return sb;
        }

        public static StringBuilder EnsureEndsWithWhiteSpace(this StringBuilder sb, int whiteSpaceCount = 1) {
            if (sb.Length == 0) {
                return sb;
            }

            for (var i = sb.Length - 1; i >= 0 && whiteSpaceCount > 0 && char.IsWhiteSpace(sb[i]); i--) {
                whiteSpaceCount--;
            }

            if (whiteSpaceCount > 0) {
                sb.Append(' ', whiteSpaceCount);
            }

            return sb;
        }

        public static StringBuilder AppendIf(this StringBuilder sb, bool condition, char value) {
            if (condition) {
                sb.Append(value);
            }

            return sb;
        }

        public static StringBuilder AppendIf(this StringBuilder sb, bool condition, string value) {
            if (condition) {
                sb.Append(value);
            }

            return sb;
        }

        public static StringBuilder Append(this StringBuilder sb, string separator, IReadOnlyList<string> values) 
            => sb.Append(separator, values, 0, values.Count);

        public static StringBuilder Append(this StringBuilder sb, string separator, IReadOnlyList<string> values, int start, int length) {
            if (length <= 0 || values.Count < start + length) {
                return sb;
            }

            sb.Append(values[start]);

            for (var i = start + 1; i < start + length; i++) {
                sb.Append(separator).Append(values[i]);
            }

            return sb;
        }
    }
}

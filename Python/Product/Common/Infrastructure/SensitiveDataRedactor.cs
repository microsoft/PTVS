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
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Infrastructure {
    static class SensitiveDataRedactor {
        internal const string RedactedValue = "<redacted>";

        private static readonly Regex AuthorizationHeader = new Regex(
            @"(?im)(\bAuthorization\s*:\s*)([^\r\n]+)",
            RegexOptions.CultureInvariant
        );

        private static readonly Regex UriUserInfo = new Regex(
            @"(?i)\b([a-z][a-z0-9+.-]*://)([^/\s:@]+(?::[^/\s@]*)?@)",
            RegexOptions.CultureInvariant
        );

        private static readonly Regex SecretKeyValue = new Regex(
            @"(?ix)(\b(?:password|passwd|pwd|token|access[_-]?token|refresh[_-]?token|secret|client[_-]?secret|api[_-]?key|subscription[_-]?key|credential|authorization|connection\s*string|connectionstring|sharedaccesssignature|sig|key)\b\s*[:=]\s*)(['""']?)([^'"";\s,&]+)(['""']?)",
            RegexOptions.CultureInvariant
        );

        public static string Sanitize(string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            var sanitized = AuthorizationHeader.Replace(text, "$1" + RedactedValue);
            sanitized = UriUserInfo.Replace(sanitized, "$1" + RedactedValue + "@");
            sanitized = SecretKeyValue.Replace(sanitized, match =>
                match.Groups[1].Value + match.Groups[2].Value + RedactedValue + match.Groups[4].Value
            );
            return sanitized;
        }
    }
}
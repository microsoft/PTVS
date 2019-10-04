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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Django.Analysis {
    internal static class DjangoNestedTags {
        internal static readonly Dictionary<string, string> _nestedTags = new Dictionary<string, string>() {
            { "for", "endfor" },
            { "if", "endif" },
            { "ifequal", "endifequal" },
            { "ifnotequal", "endifnotequal" },
            { "ifchanged", "endifchanged" },
            { "autoescape", "endautoescape" },
            { "comment", "endcomment" },
            { "filter", "endfilter" },
            { "spaceless", "endspaceless" },
            { "with", "endwith" },
            { "empty", "endfor" },
            { "else", "endif" },
        };

        internal static readonly HashSet<string> _nestedEndTags = MakeNestedEndTags();
        
        internal static readonly HashSet<string> _nestedStartTags = MakeNestedStartTags();
        
        private static HashSet<string> MakeNestedEndTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var value in _nestedTags.Values) {
                res.Add(value);
            }
            return res;
        }

        private static HashSet<string> MakeNestedStartTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var key in _nestedTags.Keys) {
                res.Add(key);
            }
            return res;
        }
    }
}

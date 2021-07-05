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

namespace Microsoft.CookiecutterTools.Model {
    class SearchUtils {
        internal static string[] ParseKeywords(string filter) {
            if (filter != null) {
                return filter.Split(new char[] { ' ', '\t', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            } else {
                return null;
            }
        }

        internal static bool SearchMatches(string[] keywords, Template template) {
            return SearchMatches(keywords, template.Name) || SearchMatches(keywords, template.Description);
        }

        private static bool SearchMatches(string[] keywords, string text) {
            if (text == null) {
                return false;
            }

            if (keywords == null || keywords.Length == 0) {
                return true;
            }

            foreach (var keyword in keywords) {
                if (text.Contains(keyword)) {
                    return true;
                }
            }

            return false;
        }

    }
}

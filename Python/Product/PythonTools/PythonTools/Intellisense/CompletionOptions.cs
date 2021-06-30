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

using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Intellisense {
    public class CompletionOptions {
        /// <summary>
        /// The set of options used by the analyzer.
        /// </summary>
        public GetMemberOptions MemberOptions { get; set; }

        /// <summary>
        /// Only show completions for members belonging to all potential types
        /// of the variable.
        /// </summary>
        public bool IntersectMembers {
            get { return MemberOptions.HasFlag(GetMemberOptions.IntersectMultipleResults); }
            set {
                if (value) {
                    MemberOptions |= GetMemberOptions.IntersectMultipleResults;
                } else {
                    MemberOptions &= ~GetMemberOptions.IntersectMultipleResults;
                }
            }
        }

        /// <summary>
        /// Omit completions for advanced members.
        /// </summary>
        public bool HideAdvancedMembers {
            get { return MemberOptions.HasFlag(GetMemberOptions.HideAdvancedMembers); }
            set {
                if (value) {
                    MemberOptions |= GetMemberOptions.HideAdvancedMembers;
                } else {
                    MemberOptions &= ~GetMemberOptions.HideAdvancedMembers;
                }
            }
        }


        /// <summary>
        /// Show context-sensitive completions for statement keywords.
        /// </summary>
        public bool IncludeStatementKeywords {
            get { return MemberOptions.HasFlag(GetMemberOptions.IncludeStatementKeywords); }
            set {
                if (value) {
                    MemberOptions |= GetMemberOptions.IncludeStatementKeywords;
                } else {
                    MemberOptions &= ~GetMemberOptions.IncludeStatementKeywords;
                }
            }
        }


        /// <summary>
        /// Show context-sensitive completions for expression keywords.
        /// </summary>
        public bool IncludeExpressionKeywords {
            get { return MemberOptions.HasFlag(GetMemberOptions.IncludeExpressionKeywords); }
            set {
                if (value) {
                    MemberOptions |= GetMemberOptions.IncludeExpressionKeywords;
                } else {
                    MemberOptions &= ~GetMemberOptions.IncludeExpressionKeywords;
                }
            }
        }


        /// <summary>
        /// Convert Tab characters to TabSize spaces.
        /// </summary>
        public bool ConvertTabsToSpaces { get; set; }

        /// <summary>
        /// The number of spaces each Tab character occupies.
        /// </summary>
        public int TabSize { get; set; }

        /// <summary>
        /// The number of spaces added for each level of indentation.
        /// </summary>
        public int IndentSize { get; set; }

        /// <summary>
        /// True to filter completions to those similar to the search string.
        /// </summary>
        public bool FilterCompletions { get; set; }

        public CompletionOptions() {
            MemberOptions = GetMemberOptions.IncludeStatementKeywords |
                GetMemberOptions.IncludeExpressionKeywords |
                GetMemberOptions.HideAdvancedMembers;
            FilterCompletions = true;
        }

        public CompletionOptions(GetMemberOptions options) {
            MemberOptions = options;
            FilterCompletions = true;
        }

        /// <summary>
        /// Returns a new instance of this CompletionOptions that cannot be modified
        /// by the code that provided the original.
        /// </summary>
        public CompletionOptions Clone() {
            return new CompletionOptions(MemberOptions) {
                ConvertTabsToSpaces = ConvertTabsToSpaces,
                TabSize = TabSize,
                IndentSize = IndentSize,
                FilterCompletions = FilterCompletions,
            };
        }

    }
}

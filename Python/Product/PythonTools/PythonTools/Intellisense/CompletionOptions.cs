/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba)
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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

        /// <summary>
        /// The search mode to use for completions.
        /// </summary>
        public FuzzyMatchMode SearchMode { get; set; }

        public CompletionOptions() {
            MemberOptions = GetMemberOptions.IncludeStatementKeywords |
                GetMemberOptions.IncludeExpressionKeywords |
                GetMemberOptions.HideAdvancedMembers;
            FilterCompletions = true;
            SearchMode = FuzzyMatchMode.Default;
        }

        public CompletionOptions(GetMemberOptions options) {
            MemberOptions = options;
            FilterCompletions = true;
            SearchMode = FuzzyMatchMode.Default;
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
                SearchMode = SearchMode
            };
        }

    }
}

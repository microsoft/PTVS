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

namespace Microsoft.PythonTools.Analysis {
    [Flags]
    public enum GetMemberOptions {
        None,
        /// <summary>
        /// When an expression resolves to multiple types the intersection of members is returned.  When this flag
        /// is not present the union of all members is returned.
        /// </summary>
        IntersectMultipleResults = 0x01,

        /// <summary>
        /// True if advanced members (currently defined as name mangled private members) should be hidden.
        /// </summary>
        HideAdvancedMembers = 0x02,

        /// <summary>
        /// True if the members should include keywords which only show up in a statement context that can 
        /// be completed in the current context in addition to the member list.  
        /// 
        /// Keywords are only included when the request for completions is for all top-level members 
        /// during a call to ModuleAnalysis.GetMembersByIndex or when specifically requesting top 
        /// level members via ModuleAnalysis.GetAllAvailableMembersByIndex.
        /// </summary>
        IncludeStatementKeywords = 0x04,

        /// <summary>
        /// True if the members should include keywords which can show up in an expression context that
        /// can be completed in the current context in addition to the member list.  
        /// 
        /// Keywords are only included when the request for completions is for all top-level members 
        /// during a call to ModuleAnalysis.GetMembersByIndex or when specifically requesting top 
        /// level members via ModuleAnalysis.GetAllAvailableMembersByIndex.
        /// </summary>
        IncludeExpressionKeywords = 0x08,
    }

    internal static class GetMemberOptionsExtensions {
        public static bool Intersect(this GetMemberOptions self) {
            return (self & GetMemberOptions.IntersectMultipleResults) != 0;
        }
        public static bool HideAdvanced(this GetMemberOptions self) {
            return (self & GetMemberOptions.HideAdvancedMembers) != 0;
        }
        public static bool Keywords(this GetMemberOptions self) {
            return (self & GetMemberOptions.IncludeStatementKeywords  | GetMemberOptions.IncludeExpressionKeywords) != 0;
        }
        public static bool StatementKeywords(this GetMemberOptions self) {
            return (self & GetMemberOptions.IncludeStatementKeywords) != 0;
        }
        public static bool ExpressionKeywords(this GetMemberOptions self) {
            return (self & GetMemberOptions.IncludeExpressionKeywords) != 0;
        }
    }
}

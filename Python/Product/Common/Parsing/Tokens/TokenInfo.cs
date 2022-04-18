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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing {

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")] // TODO: fix
    [Serializable]
    public struct TokenInfo : IEquatable<TokenInfo> {
        public TokenCategory Category { get; set; }
        public TokenTriggers Trigger { get; set; }
        public SourceSpan SourceSpan { get; set; }

        internal TokenInfo(SourceSpan span, TokenCategory category, TokenTriggers trigger) {
            Category = category;
            Trigger = trigger;
            SourceSpan = span;
        }

        #region IEquatable<TokenInfo> Members
        public bool Equals(TokenInfo other) 
            => Category == other.Category && Trigger == other.Trigger && SourceSpan == other.SourceSpan;
        #endregion

        public override string ToString() => "TokenInfo: {0}, {1}, {2}".FormatInvariant(SourceSpan, Category, Trigger);
    }
}

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

namespace Microsoft.PythonTools.Django.TemplateParsing
{
    struct TemplateToken : IEquatable<TemplateToken>
    {
        internal readonly TemplateTokenKind Kind;
        internal readonly int Start, End;
        internal readonly bool IsClosed;

        public TemplateToken(TemplateTokenKind kind, int start, int end, bool isClosed = true)
        {
            Kind = kind;
            Start = start;
            End = end;
            IsClosed = isClosed;
        }

        public override bool Equals(object obj)
        {
            if (obj is TemplateToken)
            {
                return Equals((TemplateToken)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() ^ Start ^ End ^ IsClosed.GetHashCode();
        }

        #region IEquatable<TemplateToken> Members

        public bool Equals(TemplateToken other)
        {
            return Kind == other.Kind &&
                Start == other.Start &&
                End == other.End &&
                IsClosed == other.IsClosed;
        }

        #endregion
    }
}

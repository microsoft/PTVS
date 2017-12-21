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

using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    public sealed class ChangeInfo {
        public string InsertedText { get; set; }
        public SourceSpan ReplacedSpan { get; set; }

        public static ChangeInfo Insert(string text, SourceLocation start) {
            return new ChangeInfo { InsertedText = text, ReplacedSpan = new SourceSpan(start, start) };
        }

        public static ChangeInfo Insert(PythonAst tree, string text, int start) => Insert(text, tree.IndexToLocation(start));

        public static ChangeInfo Delete(SourceSpan span) {
            return new ChangeInfo {
                InsertedText = null,
                ReplacedSpan = span
            };
        }

        public static ChangeInfo Delete(SourceLocation start, SourceLocation end) => Delete(new SourceSpan(start, end));

        public static ChangeInfo Delete(PythonAst tree, int start, int length) => Delete(tree.IndexToLocation(start), tree.IndexToLocation(start + length));

        public static ChangeInfo Replace(SourceSpan span, string text) {
            return new ChangeInfo {
                InsertedText = text,
                ReplacedSpan = span
            };
        }

        public static ChangeInfo Replace(SourceLocation start, SourceLocation end, string text) => Replace(new SourceSpan(start, end), text);

        public static ChangeInfo Replace(PythonAst tree, int start, int length, string text) => Replace(new SourceSpan(tree.IndexToLocation(start), tree.IndexToLocation(start + length)), text);
    }
}

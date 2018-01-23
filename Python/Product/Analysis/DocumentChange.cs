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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public sealed class DocumentChangeSet {
        public DocumentChangeSet(int fromVersion, int toVersion, IEnumerable<DocumentChange> changes) {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            Changes = changes.ToArray();
        }

        public int FromVersion { get; }
        public int ToVersion { get; }
        public IReadOnlyCollection<DocumentChange> Changes { get; }
    }

    public sealed class DocumentChange {
        public string InsertedText { get; set; }
        public SourceSpan ReplacedSpan { get; set; }
        public bool WholeBuffer { get; set; }

        public static DocumentChange Insert(string text, SourceLocation start) {
            return new DocumentChange { InsertedText = text, ReplacedSpan = new SourceSpan(start, start) };
        }

        public static DocumentChange Insert(PythonAst tree, string text, int start) => Insert(text, tree.IndexToLocation(start));

        public static DocumentChange Delete(SourceSpan span) {
            return new DocumentChange {
                InsertedText = null,
                ReplacedSpan = span
            };
        }

        public static DocumentChange Delete(SourceLocation start, SourceLocation end) => Delete(new SourceSpan(start, end));

        public static DocumentChange Delete(PythonAst tree, int start, int length) => Delete(tree.IndexToLocation(start), tree.IndexToLocation(start + length));

        public static DocumentChange Replace(SourceSpan span, string text) {
            return new DocumentChange {
                InsertedText = text,
                ReplacedSpan = span
            };
        }

        public static DocumentChange Replace(SourceLocation start, SourceLocation end, string text) => Replace(new SourceSpan(start, end), text);

        public static DocumentChange Replace(PythonAst tree, int start, int length, string text) => Replace(new SourceSpan(tree.IndexToLocation(start), tree.IndexToLocation(start + length)), text);
    }
}

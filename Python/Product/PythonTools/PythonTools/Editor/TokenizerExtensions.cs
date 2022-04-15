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

using Microsoft.Python.Core.Text;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Editor {
    internal static class TokenizerExtensions {
        public static LineTokenization TokenizeLine(this Tokenizer tokenizer, ITextSnapshotLine line, object state) {
            tokenizer.Initialize(
                state,
                new SnapshotSpanSourceCodeReader(line.ExtentIncludingLineBreak),
                new SourceLocation(line.Start.Position, line.LineNumber + 1, 1)
            );

            try {
                return new LineTokenization(
                    tokenizer.ReadTokens(line.LengthIncludingLineBreak),
                    tokenizer.CurrentState,
                    line
                );
            } finally {
                tokenizer.Uninitialize();
            }
        }
    }
}

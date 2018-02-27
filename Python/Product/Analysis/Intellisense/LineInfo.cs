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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    struct LineInfo {
        public LineInfo(int start, int length, int lineNo, NewLineKind lineBreak) {
            Start = start;
            Length = length;
            LineNo = lineNo;
            LineBreak = lineBreak;
        }

        public int Start { get; }
        public int Length { get; }

        /// <summary>
        /// 0-based line number
        /// </summary>
        public int LineNo { get; }

        public NewLineKind LineBreak { get; }

        public int End => Start + Length;
        public int EndIncludingLineBreak => End + LineBreak.GetSize();

        public SourceLocation SourceStart => new SourceLocation(LineNo + 1, 1);
        public SourceLocation SourceEnd => new SourceLocation(LineNo + 1, Length + 1);
        public SourceLocation SourceEndIncludingLineBreak => new SourceLocation(LineNo + 1, Length + LineBreak.GetSize() + 1);
        public SourceSpan SourceExtent => new SourceSpan(SourceStart, SourceEnd);


        public static IEnumerable<LineInfo> SplitLines(string text, int firstLineNumber = 0) {
            NewLineLocation nextLine;
            int lineNo = firstLineNumber;

            int lastLineEnd = 0;
            while ((nextLine = NewLineLocation.FindNewLine(text, lastLineEnd)).EndIndex != lastLineEnd) {
                yield return new LineInfo(
                    lastLineEnd,
                    nextLine.EndIndex - lastLineEnd - nextLine.Kind.GetSize(),
                    lineNo++,
                    nextLine.Kind
                );

                lastLineEnd = nextLine.EndIndex;
            }

            if (lastLineEnd != text.Length) {
                yield return new LineInfo(
                    lastLineEnd,
                    text.Length - lastLineEnd,
                    lineNo++,
                    NewLineKind.None
                );

            }
        }
    }
}

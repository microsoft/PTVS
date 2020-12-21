// Visual Studio Shared Project
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestUtilities.Mocks
{
    public class MockTextSnapshot : ITextSnapshot
    {
        private readonly string _text;
        private string[] _lines;
        private readonly MockTextBuffer _buffer;
        private readonly MockTextVersion _version;

        public MockTextSnapshot(MockTextBuffer buffer, string text)
        {
            _text = text;
            _buffer = buffer;
            _version = new MockTextVersion(0, this);
        }

        public MockTextSnapshot(MockTextBuffer buffer, string text, MockTextSnapshot prevVersion, params ITextChange[] changes)
        {
            _text = text;
            _buffer = buffer;
            _version = new MockTextVersion(prevVersion.Version.VersionNumber + 1, this);
            ((MockTextVersion)prevVersion.Version).SetNext(_version, changes);
        }

        public Microsoft.VisualStudio.Utilities.IContentType ContentType
        {
            get { return _buffer.ContentType; }
        }

        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            _text.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            return new MockTrackingPoint(this, position, trackingMode, trackingFidelity);
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode)
        {
            return new MockTrackingPoint(this, position, trackingMode);
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode)
        {
            return new MockTrackingSpan(this, start, length, trackingMode);
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode)
        {
            return new MockTrackingSpan(this, span.Start, span.Length, trackingMode);
        }

        private string[] GetLines()
        {
            return _lines = _lines ?? _text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        public ITextSnapshotLine GetLineFromLineNumber(int lineNumber)
        {
            for (int curLine = 0, curPosition = 0; ; curLine++)
            {
                int endOfLine = _text.IndexOfAny(new[] { '\r', '\n' }, curPosition);
                string eolChar = GetEolChar(endOfLine);
                if (curLine == lineNumber)
                {
                    return new MockTextSnapshotLine(
                        this,
                        endOfLine == -1 ?
                            _text.Substring(curPosition) :
                            _text.Substring(curPosition, endOfLine - curPosition),
                        lineNumber,
                        curPosition,
                        eolChar
                    );
                }
                if (endOfLine == -1)
                {
                    throw new ArgumentOutOfRangeException($"Line {lineNumber} not in snapshot \"{_text}\"");
                }
                curPosition = endOfLine + eolChar.Length;
            }
        }

        private string GetEolChar(int endOfLine)
        {
            if (endOfLine != -1)
            {
                if (_text[endOfLine] == '\r')
                {
                    if ((endOfLine + 1) < _text.Length && _text[endOfLine + 1] == '\n')
                    {
                        return "\r\n";
                    }
                    return "\r";
                }
                else if (_text[endOfLine] == '\n')
                {
                    return "\n";
                }
            }
            return "";
        }

        public ITextSnapshotLine GetLineFromPosition(int position)
        {
            int lineNo = 0;
            int curPos = 0;
            while (curPos < position)
            {
                curPos = _text.IndexOf('\n', curPos);
                if (curPos == -1 || curPos >= position)
                {
                    var res = GetLineFromLineNumber(lineNo);
                    Debug.Assert(position >= res.Start);
                    Debug.Assert(position <= res.EndIncludingLineBreak);
                    return res;
                }
                if (_text[curPos] == '\n')
                {
                    curPos += 1;
                }
                else
                {
                    curPos += 2;
                }
                lineNo++;
            }

            return GetLineFromLineNumber(lineNo);
        }

        public int GetLineNumberFromPosition(int position)
        {
            return GetLineFromPosition(position).LineNumber;
        }

        public string GetText()
        {
            return _text.ToString();
        }

        public string GetText(int startIndex, int length)
        {
            return GetText().Substring(startIndex, length);
        }

        public string GetText(Span span)
        {
            return GetText().Substring(span.Start, span.Length);
        }

        public int Length
        {
            get { return _text.Length; }
        }

        public int LineCount
        {
            get { return GetLines().Length; }
        }

        public IEnumerable<ITextSnapshotLine> Lines
        {
            get
            {
                for (int i = 0; i < LineCount; i++)
                {
                    yield return GetLineFromLineNumber(i);
                }
            }
        }

        public ITextBuffer TextBuffer
        {
            get { return _buffer; }
        }

        public char[] ToCharArray(int startIndex, int length)
        {
            throw new NotImplementedException();
        }

        public ITextVersion Version
        {
            get { return _version; }
        }

        public void Write(System.IO.TextWriter writer)
        {
            throw new NotImplementedException();
        }

        public void Write(System.IO.TextWriter writer, Span span)
        {
            throw new NotImplementedException();
        }

        public char this[int position]
        {
            get { return _text[position]; }
        }
    }
}

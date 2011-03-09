/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;

namespace AnalysisTest.Mocks {
    class MockTextSnapshot : ITextSnapshot {
        private readonly MockTextBuffer _buffer;

        public MockTextSnapshot(MockTextBuffer mockTextBuffer) {
            _buffer = mockTextBuffer;
        }

        public Microsoft.VisualStudio.Utilities.IContentType ContentType {
            get { throw new NotImplementedException(); }
        }

        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
            _buffer._text.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            throw new NotImplementedException();
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) {
            return new MockTrackingSpan(this, span.Start, span.Length);
        }

        private string[] GetLines() {
            return _buffer._text.Split(new[] { "\r\n" }, StringSplitOptions.None);
        }

        public ITextSnapshotLine GetLineFromLineNumber(int lineNumber) {
            var text = _buffer._text;
            for (int curLine = 0, curPosition = 0; ; curLine++) {
                int endOfLine = text.IndexOf('\r', curPosition);
                if (curLine == lineNumber) {
                    if (endOfLine == -1) {
                        return new MockTextSnapshotLine(this, text.Substring(curPosition), lineNumber, curPosition, false);
                    } else {
                        return new MockTextSnapshotLine(this, text.Substring(curPosition, endOfLine - curPosition), lineNumber, curPosition, true);
                    }
                }
                if (endOfLine == -1) {
                    Debug.Assert(false);
                    return null;
                }
                curPosition = endOfLine + 2;
            }
        }

        public ITextSnapshotLine GetLineFromPosition(int position) {
            var text = _buffer._text;
            int lineNo = 0;
            int curPos = 0;
            while (curPos < position) {
                curPos = text.IndexOf('\r', curPos);
                if (curPos == -1 || curPos >= position) {
                    return GetLineFromLineNumber(lineNo);
                }
                curPos += 2; // skip newline
                lineNo++;
            }

            return GetLineFromLineNumber(lineNo);
        }

        public int GetLineNumberFromPosition(int position) {
            return GetLineFromPosition(position).LineNumber;
        }

        public string GetText() {
            return _buffer._text;
        }

        public string GetText(int startIndex, int length) {
            return GetText().Substring(startIndex, length);
        }

        public string GetText(Span span) {
            return GetText().Substring(span.Start, span.Length);
        }

        public int Length {
            get { return _buffer._text.Length; }
        }

        public int LineCount {
            get { return GetLines().Length; }
        }

        public IEnumerable<ITextSnapshotLine> Lines {
            get { throw new NotImplementedException(); }
        }

        public ITextBuffer TextBuffer {
            get { return _buffer; }
        }

        public char[] ToCharArray(int startIndex, int length) {
            throw new NotImplementedException();
        }

        public ITextVersion Version {
            get { return new MockTextVersion(); }
        }

        public void Write(System.IO.TextWriter writer) {
            throw new NotImplementedException();
        }

        public void Write(System.IO.TextWriter writer, Span span) {
            throw new NotImplementedException();
        }

        public char this[int position] {
            get { return _buffer._text[position]; }
        }
    }
}

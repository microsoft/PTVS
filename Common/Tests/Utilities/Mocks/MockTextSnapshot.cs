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

namespace TestUtilities.Mocks {
    public class MockTextSnapshot : ITextSnapshot {
        private readonly string _text;
        private readonly MockTextBuffer _buffer;
        private readonly MockTextVersion _version;

        public MockTextSnapshot(MockTextBuffer buffer, string text) {
            _text = text;
            _buffer = buffer;
            _version = new MockTextVersion(0, this);
        }

        public MockTextSnapshot(MockTextBuffer buffer, string text, MockTextSnapshot prevVersion, params ITextChange[] changes) {
            _text = text;
            _buffer = buffer;
            _version = new MockTextVersion(prevVersion.Version.VersionNumber + 1, this);
            ((MockTextVersion)prevVersion.Version).SetNext(_version, changes);
        }

        public Microsoft.VisualStudio.Utilities.IContentType ContentType {
            get { throw new NotImplementedException(); }
        }

        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
            _text.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            return new MockTrackingPoint(this, position);
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) {
            return new MockTrackingPoint(this, position);
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) {
            return new MockTrackingSpan(this, start, length);
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) {
            return new MockTrackingSpan(this, span.Start, span.Length);
        }

        private string[] GetLines() {
            return _text.Split(new[] { "\r\n" }, StringSplitOptions.None);
        }

        public ITextSnapshotLine GetLineFromLineNumber(int lineNumber) {
            for (int curLine = 0, curPosition = 0; ; curLine++) {
                int endOfLine = _text.IndexOf('\r', curPosition);
                if (curLine == lineNumber) {
                    if (endOfLine == -1) {
                        return new MockTextSnapshotLine(this, _text.Substring(curPosition), lineNumber, curPosition, false);
                    } else {
                        return new MockTextSnapshotLine(this, _text.Substring(curPosition, endOfLine - curPosition), lineNumber, curPosition, true);
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
            int lineNo = 0;
            int curPos = 0;
            while (curPos < position) {
                curPos = _text.IndexOf('\n', curPos);
                if (curPos == -1 || curPos >= position) {
                    var res = GetLineFromLineNumber(lineNo);
                    Debug.Assert(position >= res.Start);
                    Debug.Assert(position <= res.EndIncludingLineBreak);
                    return res;
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
            return _text.ToString();
        }

        public string GetText(int startIndex, int length) {
            return GetText().Substring(startIndex, length);
        }

        public string GetText(Span span) {
            return GetText().Substring(span.Start, span.Length);
        }

        public int Length {
            get { return _text.Length; }
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
            get { return _version; }
        }

        public void Write(System.IO.TextWriter writer) {
            throw new NotImplementedException();
        }

        public void Write(System.IO.TextWriter writer, Span span) {
            throw new NotImplementedException();
        }

        public char this[int position] {
            get { return _text[position]; }
        }
    }
}

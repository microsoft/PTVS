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
using Microsoft.VisualStudio.Text;

namespace TestUtilities.Mocks {
    public class MockTrackingSpan : ITrackingSpan {
        private readonly int _start, _length;
        private readonly MockTextSnapshot _snapshot;

        public MockTrackingSpan(MockTextSnapshot snapshot, int start, int length) {
            _start = start;
            _length = length;
            _snapshot = snapshot;
        }

        public SnapshotPoint GetEndPoint(ITextSnapshot snapshot) {
            return new SnapshotPoint(_snapshot, _start + _length);
        }

        public Span GetSpan(ITextVersion version) {
            var current = _snapshot.Version;
            var target = version;
            if (current.VersionNumber == target.VersionNumber) {
                return new Span(_start, _length);
            } else if (current.VersionNumber > target.VersionNumber) {
                // Apply the changes in reverse
                var changesStack = new Stack<INormalizedTextChangeCollection>();

                for (var v = target; v.VersionNumber < current.VersionNumber; v = v.Next) {
                    changesStack.Push(v.Changes);
                }

                var newStart = _start;
                var newLength = _length;

                while (changesStack.Count > 0) {
                    foreach (var change in changesStack.Pop()) {
                        if (change.NewPosition < newStart) {
                            newStart -= change.Delta;
                        } else if (change.NewPosition < newStart + newLength) {
                            newLength -= change.Delta;
                        }
                    }
                }

                return new Span(newStart, newLength);
            } else {
                // Apply the changes normally
                var newStart = _start;
                var newLength = _length;

                for (var v = current; v.VersionNumber < target.VersionNumber; v = v.Next) {
                    foreach (var change in v.Changes) {
                        if (change.OldPosition < newStart) {
                            newStart += change.Delta;
                        } else if (change.OldPosition < newStart + newLength) {
                            newLength += change.Delta;
                        }
                    }
                }

                return new Span(newStart, newLength);
            }
        }

        public SnapshotSpan GetSpan(ITextSnapshot snapshot) {
            return new SnapshotSpan(snapshot, GetSpan(snapshot.Version));
        }

        public SnapshotPoint GetStartPoint(ITextSnapshot snapshot) {
            var span = GetSpan(snapshot.Version);
            return new SnapshotPoint(snapshot, span.Start);
        }

        public string GetText(ITextSnapshot snapshot) {
            var span = GetSpan(snapshot.Version);
            return snapshot.GetText(span);
        }

        public ITextBuffer TextBuffer {
            get { return _snapshot.TextBuffer; }
        }

        public TrackingFidelityMode TrackingFidelity {
            get { throw new NotImplementedException(); }
        }

        public SpanTrackingMode TrackingMode {
            get { throw new NotImplementedException(); }
        }
    }
}

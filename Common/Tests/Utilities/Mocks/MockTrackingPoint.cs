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
    public class MockTrackingPoint : ITrackingPoint {
        private readonly int _position;
        private readonly MockTextSnapshot _snapshot;

        public MockTrackingPoint(MockTextSnapshot snapshot, int position) {
            _position = position;
            _snapshot = snapshot;
        }

        private SnapshotPoint GetPoint(ITextVersion version) {
            var current = _snapshot.Version;
            var target = version;
            if (current.VersionNumber == target.VersionNumber) {
                return new SnapshotPoint(_snapshot, _position);
            } else if (current.VersionNumber > target.VersionNumber) {
                // Apply the changes in reverse
                var changesStack = new Stack<INormalizedTextChangeCollection>();

                for (var v = target; v.VersionNumber < current.VersionNumber; v = v.Next) {
                    changesStack.Push(v.Changes);
                }

                var newPos = _position;

                while (changesStack.Count > 0) {
                    foreach (var change in changesStack.Pop()) {
                        if (change.NewPosition <= newPos) {
                            newPos -= change.Delta;
                        }
                    }
                }

                return new SnapshotPoint(_snapshot, newPos);
            } else {
                // Apply the changes normally
                var newPos = _position;

                for (var v = current; v.VersionNumber < target.VersionNumber; v = v.Next) {
                    foreach (var change in v.Changes) {
                        if (change.OldPosition < newPos) {
                            newPos += change.Delta;
                        }
                    }
                }

                return new SnapshotPoint(_snapshot, newPos);
            }
        }

        public SnapshotPoint GetPoint(ITextSnapshot snapshot) {
            return GetPoint(snapshot.Version);
        }

        public char GetCharacter(ITextSnapshot snapshot) {
            return GetPoint(snapshot.Version).GetChar();
        }

        public int GetPosition(ITextVersion version) {
            return GetPoint(version).Position;
        }

        public int GetPosition(ITextSnapshot snapshot) {
            return GetPoint(snapshot).Position;
        }

        public ITextBuffer TextBuffer {
            get { return _snapshot.TextBuffer; }
        }

        public TrackingFidelityMode TrackingFidelity {
            get { throw new NotImplementedException(); }
        }

        public PointTrackingMode TrackingMode {
            get { throw new NotImplementedException(); }
        }

    }
}

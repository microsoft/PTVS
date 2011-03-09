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

namespace Microsoft.PythonTools.Analysis {
    public class LocationInfo : IEquatable<LocationInfo> {
        private readonly int _line, _column;
        private readonly IProjectEntry _entry;

        internal LocationInfo(IProjectEntry entry, int line, int column) {
            _entry = entry;
            _line = line;
            _column = column;
        }

        public IProjectEntry ProjectEntry {
            get {
                return _entry;
            }
        }

        public string FilePath {
            get { return _entry.FilePath; }
        }

        public int Line {
            get { return _line; }
        }

        public int Column {
            get {
                return _column;
            }
        }

        public override bool Equals(object obj) {
            LocationInfo other = obj as LocationInfo;
            if (other != null) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() {
            return Line.GetHashCode() ^ ProjectEntry.GetHashCode();
        }

        public bool Equals(LocationInfo other) {
            // currently we filter only to line & file - so we'll only show 1 ref per each line
            // This works nicely for get and call which can both add refs and when they're broken
            // apart you still see both refs, but when they're together you only see 1.
            return Line == other.Line &&
                ProjectEntry == other.ProjectEntry;
        }
    }
}

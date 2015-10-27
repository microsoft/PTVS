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

using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    public class LocationInfo : IEquatable<LocationInfo>, ILocationResolver {
        private readonly int _line, _column;
        private readonly IProjectEntry _entry;
        internal static LocationInfo[] Empty = new LocationInfo[0];

        private static readonly IEqualityComparer<LocationInfo> _fullComparer = new FullLocationComparer();

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

        /// <summary>
        /// Provides an IEqualityComparer that compares line, column and project entries.  By
        /// default locations are equaitable based upon only line/project entry.
        /// </summary>
        public static IEqualityComparer<LocationInfo> FullComparer {
            get{
                return _fullComparer;
            }
        }

        sealed class FullLocationComparer : IEqualityComparer<LocationInfo> {
            public bool Equals(LocationInfo x, LocationInfo y) {
                return x.Line == y.Line &&
                    x.Column == y.Column &&
                    x.ProjectEntry == y.ProjectEntry;
            }

            public int GetHashCode(LocationInfo obj) {
                return obj.Line.GetHashCode() ^ obj.Column.GetHashCode() ^ obj.ProjectEntry.GetHashCode();
            }
        }

        #region ILocationResolver Members

        LocationInfo ILocationResolver.ResolveLocation(IProjectEntry project, object location) {
            return this;
        }

        #endregion
    }
}

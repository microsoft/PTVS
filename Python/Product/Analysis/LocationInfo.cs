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
        private readonly int? _endLine, _endColumn;
        private readonly string _path;
        internal static LocationInfo[] Empty = new LocationInfo[0];

        private static readonly IEqualityComparer<LocationInfo> _fullComparer = new FullLocationComparer();

        public LocationInfo(string path, int line, int column) {
            _path = path;
            _line = line;
            _column = column;
        }

        public LocationInfo(string path, int line, int column, int? endLine, int? endColumn) {
            _path = path;
            _line = line;
            _column = column;
            _endLine = endLine;
            _endColumn = endColumn;
        }

        public string FilePath {
            get { return _path; }
        }

        public int StartLine {
            get { return _line; }
        }

        public int StartColumn {
            get {
                return _column;
            }
        }

        public int? EndLine => _endLine;

        public int? EndColumn => _endColumn;

        public override bool Equals(object obj) {
            LocationInfo other = obj as LocationInfo;
            if (other != null) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() {
            return StartLine.GetHashCode() ^ FilePath.GetHashCode();
        }

        public bool Equals(LocationInfo other) {
            // currently we filter only to line & file - so we'll only show 1 ref per each line
            // This works nicely for get and call which can both add refs and when they're broken
            // apart you still see both refs, but when they're together you only see 1.
            return StartLine == other.StartLine &&
                FilePath == other.FilePath;
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
                return x.StartLine == y.StartLine &&
                    x.StartColumn == y.StartColumn &&
                    x.FilePath == y.FilePath &&
                    x.EndLine == y.EndLine &&
                    x.EndColumn == x.EndColumn;
            }

            public int GetHashCode(LocationInfo obj) {
                return obj.StartLine.GetHashCode() ^ obj.StartColumn.GetHashCode() ^ obj.FilePath.GetHashCode();
            }
        }

        #region ILocationResolver Members

        LocationInfo ILocationResolver.ResolveLocation(object location) {
            return this;
        }

        #endregion
    }
}

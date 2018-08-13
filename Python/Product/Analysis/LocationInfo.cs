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
        internal static readonly LocationInfo[] Empty = new LocationInfo[0];
        private static readonly IEqualityComparer<LocationInfo> _fullComparer = new FullLocationComparer();
        public LocationInfo(string path, Uri documentUri, int line, int column) :
             this(path, documentUri, line, column, null, null) {
        }

        public LocationInfo(string path, Uri documentUri, int line, int column, int? endLine, int? endColumn) {
            FilePath = path;
            DocumentUri = documentUri;
            StartLine = line;
            StartColumn = column;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public string FilePath { get; }

        public Uri DocumentUri { get; }

        public int StartLine { get; }

        public int StartColumn { get; }

        public int? EndLine { get; }

        public int? EndColumn { get; }

        public SourceSpan Span => new SourceSpan(
            new SourceLocation(StartLine, StartColumn),
            new SourceLocation(EndLine ?? StartLine, EndColumn ?? StartColumn)
        );

        public override bool Equals(object obj) => Equals(obj as LocationInfo);

        public override int GetHashCode() {
            return StartLine.GetHashCode() ^ (FilePath?.GetHashCode() ?? 0);
        }

        public bool Equals(LocationInfo other) {
            if (other == null) {
                return false;
            }

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
        public static IEqualityComparer<LocationInfo> FullComparer => _fullComparer;

        sealed class FullLocationComparer : IEqualityComparer<LocationInfo> {
            public bool Equals(LocationInfo x, LocationInfo y) {
                return x.StartLine == y.StartLine &&
                    x.StartColumn == y.StartColumn &&
                    x.FilePath == y.FilePath &&
                    x.EndLine == y.EndLine &&
                    x.EndColumn == x.EndColumn;
            }

            public int GetHashCode(LocationInfo obj) {
                return obj.StartLine.GetHashCode() ^ obj.StartColumn.GetHashCode() ^ (obj.FilePath?.GetHashCode() ?? 0);
            }
        }

        #region ILocationResolver Members

        LocationInfo ILocationResolver.ResolveLocation(object location) {
            return this;
        }

        ILocationResolver ILocationResolver.GetAlternateResolver() => null;

        #endregion
    }
}

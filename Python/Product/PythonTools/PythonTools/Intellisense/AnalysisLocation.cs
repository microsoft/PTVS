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

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a location that comes out of an analysis.  Includes a file, line, and column
    /// number.
    /// </summary>
    public sealed class AnalysisLocation : IEquatable<AnalysisLocation> {
        internal readonly AnalysisEntry Analysis;
        public readonly int Line, Column;
        private static readonly IEqualityComparer<AnalysisLocation> _fullComparer = new FullLocationComparer();

        internal AnalysisLocation(AnalysisEntry analysis, int line, int column) {
            Analysis = analysis;
            Line = line;
            Column = column;
        }

        public string FilePath {
            get {
                return Analysis.Path;
            }
        }

        internal void GotoSource(IServiceProvider serviceProvider) {
            string zipFileName = VsProjectAnalyzer.GetZipFileName(Analysis);
            if (zipFileName == null) {
                PythonToolsPackage.NavigateTo(
                    serviceProvider,
                    Analysis.Path,
                    Guid.Empty,
                    Line - 1,
                    Column - 1
                );
            }
        }


        public override bool Equals(object obj) {
            AnalysisLocation other = obj as AnalysisLocation;
            if (other != null) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() {
            return Line.GetHashCode() ^ Analysis.GetHashCode();
        }

        public bool Equals(AnalysisLocation other) {
            // currently we filter only to line & file - so we'll only show 1 ref per each line
            // This works nicely for get and call which can both add refs and when they're broken
            // apart you still see both refs, but when they're together you only see 1.
            return Line == other.Line &&
                Analysis == other.Analysis;
        }

        /// <summary>
        /// Provides an IEqualityComparer that compares line, column and project entries.  By
        /// default locations are equaitable based upon only line/project entry.
        /// </summary>
        public static IEqualityComparer<AnalysisLocation> FullComparer {
            get {
                return _fullComparer;
            }
        }

        sealed class FullLocationComparer : IEqualityComparer<AnalysisLocation> {
            public bool Equals(AnalysisLocation x, AnalysisLocation y) {
                return x.Line == y.Line &&
                    x.Column == y.Column &&
                    x.Analysis == y.Analysis;
            }

            public int GetHashCode(AnalysisLocation obj) {
                return obj.Line.GetHashCode() ^ obj.Column.GetHashCode() ^ obj.Analysis.GetHashCode();
            }
        }
    }
}

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

using Microsoft.PythonTools.Analysis.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a location that comes out of an analysis.  Includes a file, line, and column
    /// number.
    /// </summary>
    public sealed class AnalysisLocation : IEquatable<AnalysisLocation> {
        public readonly SourceSpan Span;
        public readonly SourceSpan? DefinitionSpan;
        private static readonly IEqualityComparer<AnalysisLocation> _fullComparer = new FullLocationComparer();

        internal AnalysisLocation(
            string filePath,
            Uri documentUri,
            SourceSpan span,
            SourceSpan? definitionSpan
        ) {
            FilePath = filePath;
            DocumentUri = documentUri;
            Span = span;
            DefinitionSpan = definitionSpan;
        }

        public string FilePath { get; }

        public Uri DocumentUri { get; }

        internal void GotoSource(IServiceProvider serviceProvider) {
            if (File.Exists(FilePath)) {
                PythonToolsPackage.NavigateTo(
                    serviceProvider,
                    FilePath,
                    Guid.Empty,
                    Span.Start.Line - 1,
                    Span.Start.Column - 1
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
            return Span.Start.Line.GetHashCode() ^ FilePath.GetHashCode();
        }

        public bool Equals(AnalysisLocation other) {
            // currently we filter only to line & file - so we'll only show 1 ref per each line
            // This works nicely for get and call which can both add refs and when they're broken
            // apart you still see both refs, but when they're together you only see 1.
            return Span.Start.Line == other.Span.Start.Line &&
                String.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Provides an IEqualityComparer that compares line, column and project entries.  By
        /// default locations are equaitable based upon only line/project entry.
        /// </summary>
        public static IEqualityComparer<AnalysisLocation> FullComparer { get; } = new FullLocationComparer();

        sealed class FullLocationComparer : IEqualityComparer<AnalysisLocation> {
            public bool Equals(AnalysisLocation x, AnalysisLocation y) {
                return x.Span == y.Span &&
                    x.DefinitionSpan == y.DefinitionSpan &&
                    String.Equals(x.FilePath, y.FilePath, StringComparison.OrdinalIgnoreCase) &&
                    UriEqualityComparer.IncludeFragment.Equals(x.DocumentUri, y.DocumentUri);
            }

            public int GetHashCode(AnalysisLocation obj) {
                return obj.Span.GetHashCode() ^ (obj.DefinitionSpan?.GetHashCode() ?? 0) ^ obj.FilePath.GetHashCode();
            }
        }
    }
}

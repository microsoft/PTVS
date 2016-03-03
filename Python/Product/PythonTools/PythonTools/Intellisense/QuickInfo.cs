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
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    public sealed class QuickInfo {
        public readonly string Text;
        public readonly ITrackingSpan Span;

        public QuickInfo(string text, ITrackingSpan span) {
            Text = text;
            Span = span;
        }
    }

    public sealed class ExpressionAnalysis {
        public readonly string Text;
        public readonly ITrackingSpan Span;
        public readonly AnalysisVariable[] References;
        public readonly VsProjectAnalyzer Analyzer;
        public readonly string PrivatePrefix;
        public readonly string MemberName;

        public ExpressionAnalysis(VsProjectAnalyzer analyzer, string text, ITrackingSpan span, AnalysisVariable[] references, string privatePrefix, string memberName) {
            Analyzer = analyzer;
            Span = span;
            Text = text;
            References = references;
            PrivatePrefix = privatePrefix;
            MemberName = memberName;
        }
    }

    public sealed class AnalysisVariable {
        private readonly AnalysisLocation _loc;
        private readonly VariableType _type;

        public AnalysisVariable(VariableType type, AnalysisLocation location) {
            _loc = location;
            _type = type;
        }

        #region IAnalysisVariable Members

        public AnalysisLocation Location {
            get { return _loc; }
        }

        public VariableType Type {
            get { return _type; }
        }

        #endregion
    }

    public sealed class AnalysisLocation {
        public readonly ProjectFileInfo File;
        public readonly int Line, Column;
        private static readonly IEqualityComparer<AnalysisLocation> _fullComparer = new FullLocationComparer();

        public AnalysisLocation(ProjectFileInfo file, int line, int column) {
            File = file;
            Line = line;
            Column = column;
        }

        public string FilePath {
            get {
                return File.FilePath;
            }
        }

        internal void GotoSource(IServiceProvider serviceProvider) {
        }


        public override bool Equals(object obj) {
            LocationInfo other = obj as LocationInfo;
            if (other != null) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() {
            return Line.GetHashCode() ^ File.GetHashCode();
        }

        public bool Equals(AnalysisLocation other) {
            // currently we filter only to line & file - so we'll only show 1 ref per each line
            // This works nicely for get and call which can both add refs and when they're broken
            // apart you still see both refs, but when they're together you only see 1.
            return Line == other.Line &&
                File == other.File;
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
                    x.File == y.File;
            }

            public int GetHashCode(AnalysisLocation obj) {
                return obj.Line.GetHashCode() ^ obj.Column.GetHashCode() ^ obj.File.GetHashCode();
            }
        }
    }

}

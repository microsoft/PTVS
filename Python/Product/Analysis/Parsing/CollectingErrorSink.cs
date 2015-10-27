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

namespace Microsoft.PythonTools.Parsing {
    public class CollectingErrorSink  : ErrorSink {
        private readonly List<ErrorResult> _errors = new List<ErrorResult>();
        private readonly List<ErrorResult> _warnings = new List<ErrorResult>();

        public override void Add(string message, int[] lineLocations, int startIndex, int endIndex, int errorCode, Severity severity) {
            if (severity == Severity.Error || severity == Severity.FatalError) {
                _errors.Add(new ErrorResult(message, new SourceSpan(IndexToLocation(lineLocations, startIndex), IndexToLocation(lineLocations, endIndex))));
            } else if (severity == Severity.Warning) {
                _warnings.Add(new ErrorResult(message, new SourceSpan(IndexToLocation(lineLocations, startIndex), IndexToLocation(lineLocations, endIndex))));
            }
        }

        public SourceLocation IndexToLocation(int[] lineLocations, int index) {
            if (lineLocations == null) {
                return new SourceLocation(index, 1, 1);
            }
            int match = Array.BinarySearch(lineLocations, index);
            if (match < 0) {
                // If our index = -1, it means we're on the first line.
                if (match == -1) {
                    return new SourceLocation(index, 1, checked(index + 1));
                }
                // If we couldn't find an exact match for this line number, get the nearest
                // matching line number less than this one
                match = ~match - 1;
            }

            return new SourceLocation(index, match + 2, index - lineLocations[match] + 1);
        }

        public List<ErrorResult> Errors {
            get {
                return _errors;
            }
        }

        public List<ErrorResult> Warnings {
            get {
                return _warnings;
            }
        }
    }
}

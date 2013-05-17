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

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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Parsing {
    class CollectingErrorSink  : ErrorSink {
        public override void Add(string message, SourceSpan span, int errorCode, Severity severity) {
            if (severity == Severity.Error || severity == Severity.FatalError) {
                Errors.Add(new ErrorResult(message, span));
            } else if (severity == Severity.Warning) {
                Warnings.Add(new ErrorResult(message, span));
            }
        }

        public List<ErrorResult> Errors { get; } = new List<ErrorResult>();

        public List<ErrorResult> Warnings { get; } = new List<ErrorResult>();
    }
}

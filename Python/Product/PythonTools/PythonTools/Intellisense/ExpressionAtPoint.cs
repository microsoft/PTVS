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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using AP = Microsoft.PythonTools.Intellisense.AnalysisProtocol;

namespace Microsoft.PythonTools.Intellisense {
    internal class ExpressionAtPoint {
        public readonly string Text;
        public readonly AnalysisEntry Entry;
        public readonly ITrackingSpan Span;
        public readonly SourceSpan SourceSpan;
        public SourceLocation Location => SourceSpan.Start;

        public ExpressionAtPoint(AnalysisEntry entry, string text, ITrackingSpan span, SourceSpan sourceSpan) {
            Entry = entry;
            Text = text;
            Span = span;
            SourceSpan = sourceSpan;
        }
    }

    internal enum ExpressionAtPointPurpose : int {
        Hover = AP.ExpressionAtPointPurpose.Hover,
        Evaluate = AP.ExpressionAtPointPurpose.Evaluate,
        EvaluateMembers = AP.ExpressionAtPointPurpose.EvaluateMembers,
        FindDefinition = AP.ExpressionAtPointPurpose.FindDefinition,
        Rename = AP.ExpressionAtPointPurpose.Rename
    }

}

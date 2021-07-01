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

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Represents information about an analyzed expression.  This is returned from 
    /// AnalyzeExpression which is defined as an extension method in <see cref="Microsoft.PythonTools.Intellisense.PythonAnalysisExtensions"/>
    /// </summary>
    sealed class ExpressionAnalysis
    {
        private readonly string _expr;
        private readonly ITrackingSpan _span;
        private readonly AnalysisVariable[] _variables;
        private readonly string _privatePrefix;

        internal ExpressionAnalysis(string text, ITrackingSpan span, AnalysisVariable[] variables, string privatePrefix)
        {
            _span = span;
            _expr = text;
            _variables = variables;
            _privatePrefix = privatePrefix;
        }

        /// <summary>
        /// Gets the expression which was evaluated to create this expression analyze.
        /// 
        /// An expression analysis is usually created from a point in a buffer and this
        /// is the complete expression that the point mapped to.
        /// </summary>
        public string Expression => _expr;

        /// <summary>
        /// Gets the span associated with the expression.
        /// </summary>
        public ITrackingSpan Span => _span;

        /// <summary>
        /// Gets the list of variables which the expression refers to.  This can include
        /// references and definitions for variables, fields, etc... as well as actual
        /// values stored in those fields.
        /// </summary>
        public IReadOnlyList<AnalysisVariable> Variables => _variables;

        /// <summary>
        /// Gets the private prefix for this expression analysis.  This will be set
        /// when inside of a class where names could be mangled.
        /// </summary>
        public string PrivatePrefix => _privatePrefix;
    }
}

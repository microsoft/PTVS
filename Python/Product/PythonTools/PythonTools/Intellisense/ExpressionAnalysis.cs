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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
#if FALSE
    /// <summary>
    /// Provides the results of analyzing a simple expression.  Returned from Analysis.AnalyzeExpression.
    /// </summary>
    public class ExpressionAnalysis {
        private readonly string _expr;
        private readonly ProjectFileInfo _analysis;
        private readonly ITrackingSpan _span;
        private readonly int _index;
        private readonly VsProjectAnalyzer _analyzer;
        private readonly ITextSnapshot _snapshot;
        public static readonly ExpressionAnalysis Empty = new ExpressionAnalysis(null, "", null, 0, null, null);

        internal ExpressionAnalysis(VsProjectAnalyzer analyzer, string expression, ProjectFileInfo analysis, int index, ITrackingSpan span, ITextSnapshot snapshot) {
            _expr = expression;
            _analysis = analysis;
            _index = index;
            _span = span;
            _analyzer = analyzer;
            _snapshot = snapshot;
        }

        /// <summary>
        /// The expression which this is providing information about.
        /// </summary>
        public string Expression {
            get {
                return _expr;
            }
        }

        /// <summary>
        /// The span of the expression being analyzed.
        /// </summary>
        public ITrackingSpan Span {
            get {
                return _span;
            }
        }

        /// <summary>
        /// Gets all of the variables (storage locations) associated with the expression.
        /// </summary>
        public IEnumerable<IAnalysisVariable> Variables {
            get {
                if (_analysis != null) {
                    lock (_analyzer) {
                        return _analysis.GetVariables(_expr, TranslatedLocation);
                    }
                }
                return new IAnalysisVariable[0];
            }
        }

        /// <summary>
        /// The possible values of the expression (types, constants, functions, modules, etc...)
        /// </summary>
        public IEnumerable<AnalysisValue> Values {
            get {
                if (_analysis != null) {
                    lock (_analyzer) {
                        return _analysis.GetValues(_expr, TranslatedLocation);
                    }
                }
                return new AnalysisValue[0];
            }
        }

        public Expression GetEvaluatedExpression() {
            return Statement.GetExpression(_analysis.GetAstFromText(_expr, TranslatedLocation).Body);
        }

        /// <summary>
        /// Returns the complete PythonAst for the evaluated expression.  Calling Statement.GetExpression on the Body
        /// of the AST will return the same expression as GetEvaluatedExpression.
        /// 
        /// New in 1.1.
        /// </summary>
        /// <returns></returns>
        public PythonAst GetEvaluatedAst() {
            return _analysis.GetAstFromText(_expr, TranslatedLocation);
        }

        private SourceLocation TranslatedLocation {
            get {
                return VsProjectAnalyzer.TranslateIndex(_index, _snapshot, _analysis);
            }
        }
    }
#endif
}

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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the results of analyzing a simple expression.  Returned from Analysis.AnalyzeExpression.
    /// </summary>
    public class ExpressionAnalysis {
        private readonly string _expr;
        private readonly ModuleAnalysis _analysis;
        private readonly ITrackingSpan _span;
        private readonly int _lineNo;
        public static readonly ExpressionAnalysis Empty = new ExpressionAnalysis("", null, 0, null);
        
        internal ExpressionAnalysis(string expression, ModuleAnalysis analysis, int lineNo, ITrackingSpan span) {
            _expr = expression;
            _analysis = analysis;
            _lineNo = lineNo;
            _span = span;
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
                    return _analysis.GetVariables(_expr, _lineNo);
                }
                return new IAnalysisVariable[0];
            }
        }

        /// <summary>
        /// The possible values of the expression (types, constants, functions, modules, etc...)
        /// </summary>
        public IEnumerable<IAnalysisValue> Values {
            get {
                if (_analysis != null) {
                    return _analysis.GetValues(_expr, _lineNo);
                }
                return new IAnalysisValue[0];
            }
        }
    }
}

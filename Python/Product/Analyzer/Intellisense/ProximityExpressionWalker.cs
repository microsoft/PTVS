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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Walks the AST, and returns all expressions in the given line range that are eligible to be displayed
    /// in the Autos debugger tool window.
    /// </summary>
    /// <remarks>
    /// <para>The following expressions are considered eligible:</para>
    /// <list type="bullet">
    /// <item>standalone names: <c>x</c></item>
    /// <item>member access, so long as the target is side-effect free: <c>x.y</c>, <c>123.real</c>, <c>(x + 1).imag</c></item>
    /// <item>indexing/slicing, so long as target and index are side-effect free: <c>x.y[z]</c>, <c>('abc' + 'd')[1:len(s)-1]</c></item>
    /// <item>function calls, if function is one of the several hardcoded builtin functions (e.g. <c>abs</c>, <c>len</c>, <c>str</c>, <c>repr</c>),
    /// and all arguments are side-effect free: <c>len('a' * n)</c></item>
    /// </list>
    /// <para>
    /// All error-free expressions are considered side-effect-free except those involving function calls, backquotes, <c>yield</c>
    /// or <c>yield from</c>. Function calls are considered side-effect-free if they are eligible according to the definition above.
    /// </para>
    /// <para>
    /// For member access and indexing, if an expression is eligible, all its direct nested targets become non-eligible. For example,
    /// given <c>a.b.c</c>, neither <c>a</c> nor <c>a.b</c> are eligible.
    /// </para>
    /// <para>
    /// For function calls, the immediate target becomes ineligible. For example, given <c>a.b.c(d)</c>, <c>a.b.c</c> is not eligible,
    /// but <c>a.b</c> is (and hence <c>a</c> is not, per the earlier rule).
    /// </para>
    /// </remarks>
    public class ProximityExpressionWalker : PythonWalker {
        private readonly PythonAst _ast;
        private readonly int _startLine, _endLine;

        // Keys are expressions that have been walked and found to be eligible.
        // For a given key, a value is either null, or an expression that was removed from keys when this key was
        // added, because it is a subexpression of this key that we wanted to exclude. For example, given A.B.C,
        // we will first walk A and add it as a key with null as value; then walk A.B, remove A, and add A.B with
        // A as value; then walk A.B.C, remove A.B, and add A.B.C with A.B as value. 
        // The information about the excluded node is used by PostWalk(CallExpression).
        private readonly Dictionary<Expression, Expression> _expressions = new Dictionary<Expression, Expression>();

        public ProximityExpressionWalker(PythonAst ast, int startLine, int endLine) {
            _ast = ast;
            _startLine = startLine;
            _endLine = endLine;
        }

        public override void PostWalk(NameExpression node) {
            if (IsInRange(node)) {
                if(_ast.LanguageVersion.Is2x()) {
                    // In 2.7 True and False are constants, we made an exception to not show them Autos window.
                    if(node.Name == "True" || node.Name == "False") {
                        return;
                    }
                }

                _expressions.Add(node, null);
            }

        }

        public override void PostWalk(MemberExpression node) {
            if (IsInRange(node) && IsValidTarget(node.Target)) {
                _expressions.Remove(node.Target);
                _expressions.Add(node, node.Target);
            }
        }

        public override void PostWalk(IndexExpression node) {
            if (IsInRange(node) && IsValidTarget(node.Target) && IsValidTarget(node.Index)) {
                _expressions.Remove(node.Target);
                _expressions.Add(node, node.Target);
            }
        }

        public override void PostWalk(CallExpression node) {
            if (IsInRange(node) && node.Target != null) {
                // For call nodes, we don't want either the call nor the called function to show up,
                // but if it is a method, then we do want to show the object on which it is called.
                // For example, given A.B.C(42), we want A.B to show. By the time we get here, we
                // already have A.B.C in the list, so we need to remove it and reinstate A.B, which
                // will be stored as a value in the dictionary with A.B.C as key.
                Expression oldNode;
                _expressions.TryGetValue(node.Target, out oldNode);
                _expressions.Remove(node.Target);
                if (oldNode != null) {
                    _expressions.Add(oldNode, null);
                }

                // Hardcode some commonly used side-effect-free builtins to show even in calls.
                var name = node.Target as NameExpression;
                if (name != null && DetectSideEffectsWalker.IsSideEffectFreeCall(name.Name) && node.Args.All(arg => IsValidTarget(arg.Expression))) {
                    _expressions.Add(node, null);
                }
            }
        }

        private bool IsInRange(Expression node) {
            var span = node.GetSpan(_ast);
            int isct0 = Math.Max(span.Start.Line, _startLine);
            int isct1 = Math.Min(span.End.Line, _endLine);
            return isct0 <= isct1;
        }

        private bool IsValidTarget(Node node) {
            if (node == null || node is ConstantExpression || node is NameExpression) {
                return true;
            }

            var expr = node as Expression;
            if (expr != null && _expressions.ContainsKey(expr)) {
                return true;
            }

            var walker = new DetectSideEffectsWalker();
            node.Walk(walker);
            return !walker.HasSideEffects;
        }

        public IEnumerable<string> GetExpressions() {
            return _expressions.Keys.Select(expr => expr.ToCodeString(_ast, CodeFormattingOptions.Traditional).Trim()).OrderBy(s => s).Distinct();
        }
    }
}

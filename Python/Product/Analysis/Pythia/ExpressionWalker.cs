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
using System.Linq;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Pythia {
    /// <summary>
    /// AST Expression walker for expression assignments
    /// </summary>
    sealed class ExpressionWalker : PythonWalker {
        private IList<KeyValuePair> Assignments;
        private List<KeyValuePair> MethodInvocations;

        private IList<Tuple<int, int>> ConditionalRanges;
        private IList<Tuple<int, int>> LoopRanges;

        public IDictionary<int, string> EndIndexTypeNameMap { get; }

        private int CurrentPosition;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="assignments"></param>
        public ExpressionWalker(IList<KeyValuePair> assignments, int position) {
            Assignments = assignments;
            MethodInvocations = new List<KeyValuePair>();
            ConditionalRanges = new List<Tuple<int, int>>();
            LoopRanges = new List<Tuple<int, int>>();
            EndIndexTypeNameMap = new Dictionary<int, string>();
            CurrentPosition = position;
        }

        /// <summary>
        /// Handle member expression walks
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(MemberExpression node) {
            base.PostWalk(node);

            var target = node.Target;
            HandleMemberExpression(node.Name, target, string.Empty);
        }

        public override void PostWalk(NameExpression node) {
            base.PostWalk(node);

            if (node.EndIndex == CurrentPosition - 1 || node.EndIndex == CurrentPosition) {
                EndIndexTypeNameMap[CurrentPosition - 1] = Helper.ResolveVariable(Assignments, node.Name);
            }

        }


        /// <summary>
        /// Add ranges for IfStatement evaluation
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(IfStatement node) {
            base.PostWalk(node);

            ConditionalRanges.Add(new Tuple<int, int>(node.StartIndex, node.Tests[0].HeaderIndex)); // if (....) 

        }

        public bool IsCurrentPositionInConditional() {
            var isInConditional = false;

            // Check isInConditional

            foreach (var r in ConditionalRanges) {
                if (CurrentPosition > r.Item1 && CurrentPosition < r.Item2) {
                    isInConditional = true;
                    break;
                }
            }

            return isInConditional;
        }

        public List<string> GetPreviousInvocations(string type, int maxSeqLength) {
            MethodInvocations.Sort(delegate (KeyValuePair item1, KeyValuePair item2) { return item1.SpanStart.CompareTo(item2.SpanStart); });
            var prevInvocations = MethodInvocations.Where(item => item.SpanStart < CurrentPosition - 1).Reverse();

            var prevMethods = new List<string>();
            foreach (var prevInvoc in prevInvocations) {
                if (prevMethods.Count >= maxSeqLength) break;
                if (prevInvoc.Key == type && prevInvoc.Value != null) {
                    prevMethods.Add(prevInvoc.Value);
                }
            }
            return prevMethods;

        }

        /// <summary>
        /// Add ranges for Conditional evaluation for for loops
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(ForStatement node) {
            base.PostWalk(node);

            ConditionalRanges.Add(new Tuple<int, int>(node.StartIndex, node.Body.StartIndex));  // for(...) 
            LoopRanges.Add(new Tuple<int, int>(node.Body.StartIndex, node.EndIndex));
        }

        /// <summary>
        /// Add ranges for Conditional evaluation for while loops
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(WhileStatement node) {
            base.PostWalk(node);

            ConditionalRanges.Add(new Tuple<int, int>(node.StartIndex, node.Body.StartIndex));  //while(...)
            LoopRanges.Add(new Tuple<int, int>(node.Body.StartIndex, node.EndIndex));
        }

        /// <summary>
        /// Recursively walk through member functions to figure out the invocations
        /// </summary>
        /// <param name="functionName">The function called</param>
        /// <param name="callTarget">The target of the function call</param>
        /// <param name="prevFunctionsCalled">Appended functions called previously</param>
        private void HandleMemberExpression(string functionName, Expression callTarget, string prevFunctionsCalled) {
            if (callTarget is NameExpression callTargetName) {
                var variableName = callTargetName.Name;
                var resolvedName = Helper.ResolveVariable(Assignments, variableName);
                if (!string.IsNullOrEmpty(resolvedName)) {
                    var key = !string.IsNullOrEmpty(prevFunctionsCalled) ? resolvedName + "." + prevFunctionsCalled : resolvedName;
                    MethodInvocations.Add(new KeyValuePair(key, functionName, callTargetName.EndIndex));
                    var index = callTargetName.EndIndex;
                    if (!string.IsNullOrEmpty(prevFunctionsCalled)) {
                        index += prevFunctionsCalled.Length + 1;
                    }
                    EndIndexTypeNameMap[index] = key;
                }
            } else if (callTarget is CallExpression c) {
                var target = c.Target;
                if (target is MemberExpression m) {
                    HandleMemberExpression(functionName, m.Target, !string.IsNullOrEmpty(prevFunctionsCalled) ? m.Name + "." + prevFunctionsCalled : m.Name);
                }
            } else if (callTarget is MemberExpression mm) {
                HandleMemberExpression(functionName, mm.Target, !string.IsNullOrEmpty(prevFunctionsCalled) ? mm.Name + "." + prevFunctionsCalled : mm.Name);
            }
        }
    }
}

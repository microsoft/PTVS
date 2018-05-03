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
using System.Linq;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Pythia {
    /// <summary>
    /// AST Walker for variable assignments
    /// </summary>
    sealed class AssignmentWalker : PythonWalker {
        /// <summary>
        /// Assignment results
        /// </summary>
        public IList<KeyValuePair> Assignments { get; }

        /// <summary>
        /// Create new assignment walker
        /// </summary>
        public AssignmentWalker() {
            Assignments = new List<KeyValuePair>();
        }

        /// <summary>
        /// Walk through assignment statements
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(AssignmentStatement node) {
            base.PostWalk(node);

            // a = "string";
            // q = a.count().bitLength()
            if (node.Left.Count > 0) {
                if (node.Left[0] is NameExpression n) {
                    var leftVariableName = n.Name;  // a

                    if (node.Right is CallExpression c) {
                        if (c.Target is MemberExpression m) {
                            HandleMemberExpression(leftVariableName, m, string.Empty);
                        }
                    }
                }
            }

            // y = open()
            if (node.Left.Count > 0) {
                if (node.Left[0] is NameExpression n) {
                    var leftVariableName = n.Name;  // y

                    if (node.Right is CallExpression c) {
                        if (c.Target is NameExpression n2) {
                            var functionName = n2.Name; // open
                            Assignments.Add(new KeyValuePair(leftVariableName, functionName));
                        }
                    }
                }
            }

            // a = 1
            if (node.Left.Count > 0) {
                if (node.Left[0] is NameExpression n) {
                    var leftVariableName = n.Name;  // a

                    if (node.Right is ConstantExpression c) {
                        if (c.Value == null) {
                            Assignments.Add(new KeyValuePair(leftVariableName, StandardVariableTypes.Null));
                        } else {
                            var valStringRep = c.Value.ToString();  // 1

                            Assignments.Add(GetStandardVariableType(leftVariableName, valStringRep));
                        }
                    } else if (node.Right is NameExpression n2) {
                        var valStringRep = n2.Name;
                        // check for assignments p = q
                        var val = Helper.ResolveVariable(Assignments, valStringRep);
                        if (string.IsNullOrEmpty(val)) {
                            // p = true
                            Assignments.Add(GetStandardVariableType(leftVariableName, valStringRep));
                        } else {
                            // q = "string"
                            // p = q
                            Assignments.Add(new KeyValuePair(leftVariableName, val));
                        }
                    } else if (node.Right is TupleExpression t) {
                        Assignments.Add(new KeyValuePair(leftVariableName, StandardVariableTypes.Tuple));
                    } else if (node.Right is ListExpression l) {
                        Assignments.Add(new KeyValuePair(leftVariableName, StandardVariableTypes.List));
                    }
                }
            }
        }

        /// <summary>
        /// Handle from-import assignments
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(FromImportStatement node) {
            base.PostWalk(node);

            if (node.Root.Names.Count() < 1) {
                return;
            }

            var rootModuleName = node.Root.Names[0].Name;

            for (var i = 0; i < node.Names.Count; i++) {
                if (node.AsNames != null) {
                    if (node.AsNames[i] != null) {
                        Assignments.Add(new KeyValuePair(node.AsNames[i].Name, rootModuleName + "." + node.Names[i].Name));
                    } else {
                        Assignments.Add(new KeyValuePair(node.Names[i].Name, rootModuleName + "." + node.Names[i].Name));
                    }
                } else {
                    Assignments.Add(new KeyValuePair(node.Names[i].Name, node.Names[i].Name));
                }
            }
        }

        /// <summary>
        /// Handle import assignments
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(ImportStatement node) {
            base.PostWalk(node);

            var importNames = node.Names;
            var importAsNames = node.AsNames;
            if (importNames.Count > 0) {
                var listOfNameExpressions = importNames[0].Names;
                if (importAsNames[0] != null) {
                    var asName = importAsNames[0].Name;
                    if (listOfNameExpressions.Count > 0) {
                        var nameExpression = listOfNameExpressions[0];

                        Assignments.Add(new KeyValuePair(asName, nameExpression.Name));
                    }
                } else {
                    foreach (var import in importNames) {
                        Assignments.Add(new KeyValuePair(import.Names[0].Name, import.Names[0].Name));
                    }
                }
            }
        }

        /// <summary>
        /// Handle assignments in cases like: 
        /// with open(IDs_list) as f:
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(WithStatement node) {
            base.PostWalk(node);

            if (node.Items.Count > 0) {
                var item = node.Items[0];

                if (item.Variable is NameExpression n) {
                    var key = n.Name;
                    //var value = n.

                    if (item.ContextManager is CallExpression c) {
                        //c.Target
                        if (c.Target is NameExpression nn) {
                            var value = nn.Name;
                            Assignments.Add(new KeyValuePair(key, value));
                        }
                    }
                }

            }
        }

        /// <summary>
        /// Handle assignments in for loops elements
        /// for p in list: p becomes list.elemenet_inside
        /// </summary>
        /// <param name="node">for statement node</param>
        public override void PostWalk(ForStatement node) {
            base.PostWalk(node);

            var elementInsideString = "element_inside";

            if (node.Left is NameExpression left) {
                if (node.List is NameExpression list) {
                    var resolvedName = Helper.ResolveVariable(Assignments, list.Name);
                    if (!string.IsNullOrEmpty(resolvedName)) {
                        Assignments.Add(new KeyValuePair(left.Name, resolvedName + "." + elementInsideString));
                    }
                } else if (node.List is CallExpression callList) {
                    var t = callList.Target;
                    if (t is MemberExpression member) {
                        HandleMemberExpression(left.Name, member, elementInsideString);
                    }
                } else if (node.List is MemberExpression member) {
                    HandleMemberExpression(left.Name, member, elementInsideString);
                }
            }
        }

        /// <summary>
        /// Handle for assignments
        /// </summary>
        /// <param name="node"></param>
        public override void PostWalk(ComprehensionFor node) {
            base.PostWalk(node);

            var key = string.Empty;
            var value = string.Empty;
            if (node.Left is NameExpression n) {
                key = n.Name;
            } else {
                return;
            }

            if (node.List is ListExpression l) {
                if (l.Items.Count > 0) {
                    var i = l.Items[0];
                    if (i is ConstantExpression c) {
                        if (c.Value == null) {
                            Assignments.Add(new KeyValuePair(key, StandardVariableTypes.Null));
                        } else {
                            var valStringRep = c.Value.ToString();  // 1
                            Assignments.Add(GetStandardVariableType(key, valStringRep));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively walk through member assignments
        /// </summary>
        /// <param name="leftVariableName">The variable to assign for</param>
        /// <param name="m">The expression statement</param>
        /// <param name="rightHandSide">Appended member calls</param>
        private void HandleMemberExpression(string leftVariableName, MemberExpression m, string rightHandSide) {
            var functionName = m.Name; // bitLength // count
            if (m.Target is NameExpression n2) {
                var functionInvokedOnName = n2.Name; // a 
                if (functionInvokedOnName.Equals("self")) {
                    return;
                }

                // if resolving an variable name based on previous definition
                var resolvedName = Helper.ResolveVariable(Assignments, functionInvokedOnName);
                functionInvokedOnName = !string.IsNullOrEmpty(resolvedName) ? resolvedName : functionInvokedOnName;

                var combineName = functionInvokedOnName + "." + functionName + (!string.IsNullOrEmpty(rightHandSide) ? "." + rightHandSide : string.Empty);
                Assignments.Add(new KeyValuePair(leftVariableName, combineName));

                return;
            } else if (m.Target is CallExpression cc) {
                if (cc.Target is MemberExpression mm) {
                    HandleMemberExpression(leftVariableName, mm, (!string.IsNullOrEmpty(rightHandSide) ? functionName + "." + rightHandSide : functionName));
                }
            } else if (m.Target is MemberExpression mmm) {
                HandleMemberExpression(leftVariableName, mmm, (!string.IsNullOrEmpty(rightHandSide) ? functionName + "." + rightHandSide : functionName));
            }
        }

        /// <summary>
        /// Determine the type of the variable and return a variable to type value pair
        /// </summary>
        /// <param name="variableName">Key</param>
        /// <param name="value">Value to determine type for</param>
        /// <returns>Key to value pair for typing</returns>
        private KeyValuePair GetStandardVariableType(string variableName, string value) {
            if (double.TryParse(value, out double val)) {
                return new KeyValuePair(variableName, StandardVariableTypes.Numeric);
            }

            if (bool.TryParse(value, out bool val2)) {
                return new KeyValuePair(variableName, StandardVariableTypes.Boolean);
            }

            return new KeyValuePair(variableName, StandardVariableTypes.String);
        }
    }

    /// <summary>
    /// Key value pair for variable name to type
    /// </summary>
    public struct KeyValuePair {
        public readonly string Key;
        public readonly string Value;
        public readonly int SpanStart;

        public KeyValuePair(string key, string value): this(key, value, 0) { }
        public KeyValuePair(string key, string value, int spanStart) {
            Key = key;
            Value = value;
            SpanStart = 0;
        }
    }
}

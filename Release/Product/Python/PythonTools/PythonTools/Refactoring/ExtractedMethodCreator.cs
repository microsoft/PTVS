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
using System.Text;
using Microsoft.PythonTools.Parsing.Ast;
using System.Diagnostics;

namespace Microsoft.PythonTools.Refactoring {
    class ExtractedMethodCreator {
        private readonly PythonAst _ast;
        private readonly ScopeStatement[] _scopes;
        private readonly List<PythonVariable> _inputVars, _outputVars;
        private readonly SelectionTarget _target;
        private readonly int _indentSize;
        private readonly bool _insertTabs;

        public ExtractedMethodCreator(PythonAst ast, ScopeStatement[] scopes, HashSet<PythonVariable> inputVariables, HashSet<PythonVariable> outputVariables, SelectionTarget target, int indentSize, bool insertTabs) {
            _ast = ast;
            _scopes = scopes;
            _inputVars = new List<PythonVariable>(inputVariables);
            _outputVars = new List<PythonVariable>(outputVariables);
            _inputVars.Sort(CompareVariables);
            _outputVars.Sort(CompareVariables);
            _target = target;
            _indentSize = indentSize;
            _insertTabs = insertTabs;
        }

        private static int CompareVariables(PythonVariable left, PythonVariable right) {
            return String.Compare(left.Name, right.Name);
        }

        public ExtractMethodResult GetExtractionResult(ExtractMethodRequest info) {
            bool isStaticMethod = false, isClassMethod = false;
            var parameters = new List<Parameter>();
            string selfParam = null;
            if (info.TargetScope is ClassDefinition) {
                var fromScope = _scopes[_scopes.Length - 1] as FunctionDefinition;
                Debug.Assert(fromScope != null);  // we don't allow extracting from classes, so we have to be coming from a function

                if (fromScope.Decorators != null) {
                    foreach (var decorator in fromScope.Decorators.Decorators) {
                        NameExpression name = decorator as NameExpression;
                        if (name != null) {
                            if (name.Name == "staticmethod") {
                                isStaticMethod = true;
                            } else if (name.Name == "classmethod") {
                                isClassMethod = true;
                            }
                        }
                    }
                }

                if (!isStaticMethod) {
                    if (fromScope.Parameters.Count > 0) {
                        selfParam = fromScope.Parameters[0].Name;
                        parameters.Add(new Parameter(selfParam, ParameterKind.Normal));
                    }
                }
            }

            foreach (var param in info.Parameters) {
                var newParam = new Parameter(param, ParameterKind.Normal);
                if (parameters.Count > 0) {
                    newParam.AddPreceedingWhiteSpace(_ast, " ");
                }
                parameters.Add(newParam);
            }

            // include any non-closed over parameters as well...
            foreach (var input in _inputVars) {
                var variableScope = input.Scope;
                var parentScope = info.TargetScope;

                // are these variables a child of the target scope so we can close over them?
                while (parentScope != null && parentScope != variableScope) {
                    parentScope = parentScope.Parent;
                }

                if (parentScope == null && input.Name != selfParam) {
                    // we can either close over or pass these in as parameters, add them to the list
                    var newParam = new Parameter(input.Name, ParameterKind.Normal);
                    if (parameters.Count > 0) {
                        newParam.AddPreceedingWhiteSpace(_ast, " ");
                    }
                    parameters.Add(newParam);
                }
            }

            var body = _target.GetBody(_ast);

            if (_outputVars.Count > 0) {
                // need to add a return statement
                Expression retValue;
                Expression[] names = new Expression[_outputVars.Count];
                int outputIndex = 0;
                foreach (var name in _outputVars) {
                    var nameExpr = new NameExpression(name.Name);
                    nameExpr.AddPreceedingWhiteSpace(_ast, " ");
                    names[outputIndex++] = nameExpr;
                }
                var tuple = new TupleExpression(false, names);
                tuple.RoundTripHasNoParenthesis(_ast);
                retValue = tuple;

                var retStmt = new ReturnStatement(retValue);
                if (body is SuiteStatement) {
                    SuiteStatement suite = (SuiteStatement)body;
                    Node.CopyLeadingWhiteSpace(_ast, suite.Statements[0], retStmt);
                    Node.CopyTrailingNewLine(_ast, suite.Statements[0], suite.Statements[suite.Statements.Count - 1]);

                    Statement[] statements = new Statement[suite.Statements.Count + 1];
                    for (int i = 0; i < suite.Statements.Count; i++) {
                        statements[i] = suite.Statements[i];
                    }
                    statements[statements.Length - 1] = retStmt;
                    body = new SuiteStatement(statements);
                } else {
                    Node.CopyLeadingWhiteSpace(_ast, body, retStmt);

                    body = new SuiteStatement(
                        new Statement[] { 
                            body,
                            retStmt
                        }
                    );
                }
            }

            DecoratorStatement decorators = null;
            if (isStaticMethod) {
                decorators = new DecoratorStatement(new[] { new NameExpression("staticmethod") });
            } else if (isClassMethod) {
                decorators = new DecoratorStatement(new[] { new NameExpression("classmethod") });
            }

            var res = new FunctionDefinition(info.Name, parameters.ToArray(), body, decorators);

            StringBuilder newCall = new StringBuilder();

            var method = res.ToCodeString(_ast);

            // fix up indentation...
            for (int curScope = 0; curScope < _scopes.Length; curScope++) {
                if (_scopes[curScope] == info.TargetScope) {
                    // this is our target indentation level.
                    var indentationLevel = _scopes[curScope].Body.GetIndentationLevel(_ast);
                    var lines = method.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    int minWhiteSpace = Int32.MaxValue;
                    for (int curLine = decorators == null ? 1 : 2; curLine < lines.Length; curLine++) {
                        var line = lines[curLine];

                        for (int i = 0; i < line.Length; i++) {
                            if (!Char.IsWhiteSpace(line[i])) {
                                minWhiteSpace = Math.Min(minWhiteSpace, i);
                                break;
                            }
                        }
                    }

                    StringBuilder newLines = new StringBuilder();
                    newLines.Append(lines[0]);
                    if (decorators != null) {
                        newLines.Append("\r\n");
                        newLines.Append(indentationLevel);
                        newLines.Append(lines[1]);
                    }

                    // don't include a bunch of blank lines...
                    int endLine = lines.Length - 1;
                    for (; endLine >= 0 && String.IsNullOrWhiteSpace(lines[endLine]); endLine--) {
                    }

                    newLines.Append("\r\n");
                    for (int curLine = decorators == null ? 1 : 2; curLine <= endLine; curLine++) {
                        var line = lines[curLine];

                        newLines.Append(indentationLevel);
                        if (_insertTabs) {
                            newLines.Append('\t');
                        } else {
                            newLines.Append(' ', _indentSize);
                        }

                        if (line.Length > minWhiteSpace) {
                            newLines.Append(line, minWhiteSpace, line.Length - minWhiteSpace);
                        }
                        newLines.Append("\r\n");
                    }
                    newLines.Append("\r\n");
                    newLines.Append(indentationLevel);
                    method = newLines.ToString();
                    break;
                }
            }

            string comma;
            if (_outputVars.Count > 0) {
                comma = "";
                foreach (var outputVar in _outputVars) {
                    newCall.Append(comma);
                    newCall.Append(outputVar.Name);
                    comma = ", ";
                }
                newCall.Append(" = ");
            } else if (_target.ContainsReturn) {
                newCall.Append("return ");
            }

            if (info.TargetScope is ClassDefinition) {
                var fromScope = _scopes[_scopes.Length - 1] as FunctionDefinition;
                Debug.Assert(fromScope != null);  // we don't allow extracting from classes, so we have to be coming from a function

                if (isStaticMethod) {
                    newCall.Append(info.TargetScope.Name);
                    newCall.Append('.');
                } else if (fromScope.Parameters.Count > 0) {
                    newCall.Append(fromScope.Parameters[0].Name);
                    newCall.Append('.');
                }
            }

            newCall.Append(info.Name);
            newCall.Append('(');

            comma = "";
            foreach (var param in parameters) {
                if (param.Name != selfParam) {
                    newCall.Append(comma);
                    newCall.Append(param.Name);
                    comma = ", ";
                }
            }

            newCall.Append(')');

            return new ExtractMethodResult(
                method,
                newCall.ToString()
            );
        }

        public ScopeStatement[] Scopes {
            get {
                return _scopes;
            }
        }

        public List<PythonVariable> Variables {
            get {
                return _inputVars;
            }
        }

        public List<PythonVariable> OutputVariables {
            get {
                return _outputVars;
            }
        }

        public string Indentation {
            get {
                if (_insertTabs) {
                    return "\t";
                } else {
                    return new string(' ', _indentSize);
                }
            }
        }
    }
}

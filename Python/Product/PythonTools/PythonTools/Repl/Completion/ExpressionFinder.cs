﻿// Copyright(c) Microsoft Corporation
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

using System.IO;
using System.Linq;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Common.Parsing.Ast;

namespace Microsoft.PythonTools.Repl.Completion {
    public sealed class ExpressionFinder {
        public ExpressionFinder(PythonAst ast, FindExpressionOptions options) {
            Ast = ast;
            Options = options;
        }

        public ExpressionFinder(string expression, PythonLanguageVersion version, FindExpressionOptions options) {
            var parser = Parser.CreateParser(new StringReader(expression), version, ParserOptions.Default);
            Ast = parser.ParseTopExpression(null);
            Ast.Body.SetLoc(0, expression.Length);
            Options = options;
        }

        public PythonAst Ast { get; }
        public FindExpressionOptions Options { get; }

        public Node GetExpression(int index) => GetExpression(index, index);
        public Node GetExpression(SourceLocation location) => GetExpression(new SourceSpan(location, location));

        public void Get(int startIndex, int endIndex, out Node node, out Node statement, out ScopeStatement scope) {
            ExpressionWalker walker;
            if (Options.Keywords) {
                walker = new KeywordWalker(Ast, startIndex, endIndex);
                Ast.Walk(walker);
                if (walker.Expression != null) {
                    node = walker.Expression;
                    statement = walker.Statement;
                    scope = walker.Scope;
                    return;
                }
            }
            if (Options.MemberTarget) {
                walker = new MemberTargetExpressionWalker(Ast, startIndex);
            } else {
                walker = new NormalExpressionWalker(Ast, startIndex, endIndex, Options);
            }
            Ast.Walk(walker);
            node = walker.Expression;
            statement = walker.Statement;
            scope = walker.Scope;
        }

        public Node GetExpression(int startIndex, int endIndex) {
            Get(startIndex, endIndex, out var expression, out _, out _);
            return expression;
        }

        public Node GetExpression(SourceSpan range) {
            var startIndex = Ast.LocationToIndex(range.Start);
            var endIndex = Ast.LocationToIndex(range.End);
            return GetExpression(startIndex, endIndex);
        }

        private abstract class ExpressionWalker : PythonWalkerWithLocation {
            protected ExpressionWalker(int location) : base(location) { }
            public Node Expression { get; protected set; }
            public Node Statement { get; protected set; }
            public ScopeStatement Scope { get; protected set; }
        }

        private class NormalExpressionWalker : ExpressionWalker {
            private readonly int _endLocation;
            private readonly FindExpressionOptions _options;

            public NormalExpressionWalker(PythonAst ast, int location, int endLocation, FindExpressionOptions options) : base(location) {
                Tree = ast;
                ExtendedStatements = true;
                _endLocation = endLocation;
                _options = options;
            }

            private bool Save(Node node, bool baseWalk, bool ifTrue) {
                if (node == null || baseWalk && !(node.StartIndex <= _endLocation && _endLocation <= node.EndIndex)) {
                    return false;
                }

                if (baseWalk && ifTrue) {
                    Expression = node is ModuleName m
                        ? m.Names.FirstOrDefault(n => n.StartIndex <= _endLocation && _endLocation <= n.EndIndex)
                        : node;
                }
                return baseWalk;
            }

            private bool SaveStmt(Statement stmt, bool baseWalk) {
                if (stmt == null) {
                    return false;
                }
                if (baseWalk) {
                    Statement = stmt;
                }

                return baseWalk;
            }

            private void ClearStmt(Statement stmt, Node body, int? headerIndex = null) {
                if (!BeforeBody(body, headerIndex)) {
                    Statement = null;
                }
            }

            private bool BeforeBody(Node body, int? headerIndex = null) {
                if (body == null || body is ErrorStatement) {
                    return true;
                }

                if (headerIndex.HasValue && Location > headerIndex.Value) {
                    return false;
                }

                if (Location >= body.StartIndex) {
                    return false;
                }

                var ws = body.GetLeadingWhiteSpace(Tree);
                if (string.IsNullOrEmpty(ws)) {
                    return true;
                }

                if (Location >= body.StartIndex - ws.Length) {
                    return false;
                }

                return true;
            }

            public override bool Walk(NameExpression node) => Save(node, base.Walk(node), _options.Names);
            public override bool Walk(DottedName node) => Save(node, base.Walk(node), _options.ImportNames);
            public override bool Walk(CallExpression node) => Save(node, base.Walk(node), _options.Calls);
            public override bool Walk(ConstantExpression node) => Save(node, base.Walk(node), _options.Literals);
            public override bool Walk(IndexExpression node) => Save(node, base.Walk(node), _options.Indexing);
            public override bool Walk(ParenthesisExpression node) => Save(node, base.Walk(node), _options.ParenthesisedExpression);
            public override bool Walk(ErrorExpression node) => Save(node, base.Walk(node), _options.Errors);

            public override bool Walk(AssignmentStatement node) => SaveStmt(node, base.Walk(node));
            public override bool Walk(ExpressionStatement node) => SaveStmt(node, base.Walk(node));
            public override bool Walk(ForStatement node) => SaveStmt(node, base.Walk(node));
            public override bool Walk(RaiseStatement node) => SaveStmt(node, base.Walk(node));
            public override bool Walk(WithStatement node) => SaveStmt(node, base.Walk(node));

            public override bool Walk(FunctionDefinition node) {
                if (!base.Walk(node)) {
                    return false;
                }

                SaveStmt(node, true);
                Scope = node;

                if (_options.FunctionDefinition && BeforeBody(node.Body)) {
                    Expression = node;
                }

                if (_options.FunctionDefinitionName) {
                    node.NameExpression?.Walk(this);
                }

                node.Decorators?.Walk(this);
                foreach (var p in node.Parameters) {
                    p?.Walk(this);
                }
                node.ReturnAnnotation?.Walk(this);

                ClearStmt(node, node.Body, node.HeaderIndex);
                node.Body?.Walk(this);

                return false;
            }

            public override bool Walk(Parameter node) {
                if (base.Walk(node)) {
                    if (node.NameExpression != null) {
                        Save(node.NameExpression, base.Walk(node.NameExpression), _options.ParameterNames);
                    }
                    return true;
                }
                return false;
            }

            public override bool Walk(MemberExpression node) {
                if (base.Walk(node)) {
                    if (_options.MemberName && Location > node.DotIndex && _endLocation <= node.EndIndex) {
                        var nameNode = new NameExpression(node.Name);
                        nameNode.SetLoc(node.NameHeader, node.EndIndex);
                        Expression = nameNode;
                        return false;
                    } else if (_options.Members) {
                        Expression = node;
                    }
                    return true;
                }
                return false;
            }

            public override bool Walk(ClassDefinition node) {
                if (!base.Walk(node)) {
                    return false;
                }

                SaveStmt(node, true);
                Scope = node;

                if (_options.ClassDefinition && BeforeBody(node.Body)) {
                    Expression = node;
                }

                if (_options.ClassDefinitionName) {
                    node.NameExpression?.Walk(this);
                }
                node.Decorators?.Walk(this);
                foreach (var b in node.Bases) {
                    b.Walk(this);
                }

                ClearStmt(node, node.Body, node.HeaderIndex);
                node.Body?.Walk(this);

                return false;
            }

            public override bool Walk(Arg node) {
                if (base.Walk(node)) {
                    node.Expression?.Walk(this);

                    var n = node.NameExpression;
                    if (_options.NamedArgumentNames && n != null && Location >= n.StartIndex && Location <= n.EndIndex) {
                        Expression = n;
                    }
                }

                return false;
            }

            public override bool Walk(ImportStatement node) {
                if (!base.Walk(node)) {
                    return false;

                }

                SaveStmt(node, true);

                if (_options.ImportNames) {
                    foreach (var n in node.Names) {
                        n?.Walk(this);
                    }
                }
                if (_options.ImportAsNames) {
                    foreach (var n in node.AsNames) {
                        n?.Walk(this);
                    }
                }

                return false;
            }

            public override bool Walk(FromImportStatement node) {
                if (!base.Walk(node)) {
                    return false;
                }

                SaveStmt(node, true);

                if (_options.ImportNames) {
                    node.Root.Walk(this);
                }

                foreach (var n in node.Names) {
                    n?.Walk(this);
                }

                foreach (var n in node.AsNames) {
                    n?.Walk(this);
                }

                return false;
            }

            public override bool Walk(TryStatement node) {
                if (!base.Walk(node)) {
                    return false;
                }

                if (Location > node.StartIndex && BeforeBody(node.Body, node.HeaderIndex)) {
                    Statement = node;
                }
                node.Body?.Walk(this);
                if (node.Handlers != null) {
                    foreach (var handler in node.Handlers) {
                        if (Location > handler.StartIndex && BeforeBody(handler.Body, handler.HeaderIndex)) {
                            Statement = handler;
                        }
                        handler.Walk(this);
                    }
                }
                node.Else?.Walk(this);
                node.Finally?.Walk(this);

                return false;
            }
        }

        private class MemberTargetExpressionWalker : ExpressionWalker {
            private readonly PythonAst _ast;

            public MemberTargetExpressionWalker(PythonAst ast, int location) : base(location) {
                _ast = ast;
            }

            public override bool Walk(MemberExpression node) {
                if (base.Walk(node)) {
                    if (Location >= node.NameHeader && Location <= node.EndIndex) {
                        Expression = node.Target;
                    }
                    return true;
                }
                return false;
            }
        }

        private class KeywordWalker : ExpressionWalker {
            private readonly int _endLocation;
            private readonly PythonAst _ast;

            public KeywordWalker(PythonAst ast, int location, int endLocation) : base(location) {
                _ast = ast;
                _endLocation = endLocation;
            }

            private bool Save(int startIndex, bool baseWalk, string keyword) {
                if (!baseWalk) {
                    return false;
                }

                if (startIndex < 0) {
                    return true;
                }

                if (_endLocation <= startIndex + keyword.Length) {
                    var ne = new NameExpression(keyword);
                    ne.SetLoc(startIndex, startIndex + keyword.Length);
                    Expression = ne;
                    return false;
                }

                return true;
            }

            private bool Save(Node node, bool baseWalk, string keyword) => Save(node.StartIndex, baseWalk, keyword);

            public override bool Walk(AndExpression node) => Save(node.AndIndex, base.Walk(node), "and");
            public override bool Walk(AssertStatement node) => Save(node, base.Walk(node), "assert");
            public override bool Walk(AwaitExpression node) => Save(node, base.Walk(node), "await");
            public override bool Walk(BreakStatement node) => Save(node, base.Walk(node), "break");
            public override bool Walk(ClassDefinition node) => Save(node, base.Walk(node), "class");
            public override bool Walk(ComprehensionIf node) => Save(node, base.Walk(node), "if");
            public override bool Walk(ContinueStatement node) => Save(node, base.Walk(node), "continue");
            public override bool Walk(DelStatement node) => Save(node, base.Walk(node), "del");
            public override bool Walk(EmptyStatement node) => Save(node, base.Walk(node), "pass");
            public override bool Walk(ExecStatement node) => Save(node, base.Walk(node), "exec");
            public override bool Walk(GlobalStatement node) => Save(node, base.Walk(node), "global");
            public override bool Walk(ImportStatement node) => Save(node, base.Walk(node), "import");
            public override bool Walk(LambdaExpression node) => Save(node, base.Walk(node), "lambda");
            public override bool Walk(NonlocalStatement node) => Save(node, base.Walk(node), "nonlocal");
            public override bool Walk(PrintStatement node) => Save(node, base.Walk(node), "print");
            public override bool Walk(OrExpression node) => Save(node.OrIndex, base.Walk(node), "or");
            public override bool Walk(RaiseStatement node) => Save(node, base.Walk(node), "raise");
            public override bool Walk(ReturnStatement node) => Save(node, base.Walk(node), "return");
            public override bool Walk(WithItem node) => Save(node.AsIndex, base.Walk(node), "as");
            public override bool Walk(YieldExpression node) => Save(node, base.Walk(node), "yield");

            public override bool Walk(BinaryExpression node) {
                if (base.Walk(node)) {
                    switch (node.Operator) {
                        case PythonOperator.In:
                            return Save(node.OperatorIndex, true, "in");
                        case PythonOperator.NotIn:
                            return Save(node.OperatorIndex, true, "not") &&
                                Save(node.GetIndexOfSecondOp(_ast), true, "in");
                        case PythonOperator.Is:
                        case PythonOperator.IsNot:
                            return Save(node.OperatorIndex, true, "is") &&
                                Save(node.GetIndexOfSecondOp(_ast), true, "not");
                    }
                    return true;
                }
                return false;
            }

            public override bool Walk(ComprehensionFor node) {
                if (base.Walk(node)) {
                    if (node.IsAsync && !Save(node, true, "async")) {
                        return false;
                    }
                    return Save(node.GetIndexOfFor(_ast), true, "for") && Save(node.GetIndexOfIn(_ast), true, "in");
                }
                return false;
            }

            public override bool Walk(ConditionalExpression node) {
                if (base.Walk(node)) {
                    return Save(node.IfIndex, true, "if") && Save(node.ElseIndex, true, "else");
                }
                return false;
            }

            public override bool Walk(ForStatement node) {
                if (base.Walk(node)) {
                    if (node.IsAsync && !Save(node, true, "async")) {
                        return false;
                    }
                    if (!Save(node.ForIndex, true, "for")) {
                        return false;
                    }
                    if (!Save(node.InIndex, true, "in")) {
                        return false;
                    }
                    return node.Else == null || Save(node.Else.StartIndex, true, "else");
                }
                return false;
            }

            public override bool Walk(FromImportStatement node) {
                if (base.Walk(node)) {
                    if (!Save(node, true, "from")) {
                        return false;
                    }
                    if (node.ImportIndex > 0) {
                        return Save(node.ImportIndex, true, "import");
                    }
                    return true;
                }
                return false;
            }

            public override bool Walk(FunctionDefinition node) {
                if (base.Walk(node)) {
                    if (node.IsCoroutine && !Save(node, true, "async")) {
                        return false;
                    }
                    if (!Save(node.GetIndexOfDef(_ast), true, "def")) {
                        return false;
                    }
                    return true;
                }
                return false;
            }


            public override bool Walk(IfStatement node)
                => base.Walk(node) && Save(node, true, "if");

            public override bool Walk(UnaryExpression node) {
                if (base.Walk(node)) {
                    if (node.Op == PythonOperator.Not) {
                        return Save(node, true, "not");
                    }
                }
                return false;
            }

            public override bool Walk(TryStatement node)
                => base.Walk(node) ? Save(node, true, "try") : base.Walk(node);

            public override bool Walk(WhileStatement node) {
                if (base.Walk(node)) {
                    if (!Save(node, true, "while")) {
                        return false;
                    }
                    return node.ElseStatement == null || Save(node.ElseStatement.StartIndex, true, "else");
                }
                return false;
            }

            public override bool Walk(WithStatement node) {
                if (base.Walk(node)) {
                    if (node.IsAsync && !Save(node, true, "async")) {
                        return false;
                    }
                    return Save(node.GetIndexOfWith(_ast), true, "with") &&
                           node.Items.MaybeEnumerate().All(item => Save(item.AsIndex, true, "as"));
                }
                return false;
            }

            public override bool Walk(YieldFromExpression node) {
                if (base.Walk(node)) {
                    return Save(node, true, "yield") && Save(node.GetIndexOfFrom(_ast), true, "from");
                }
                return false;
            }
        }
    }
}
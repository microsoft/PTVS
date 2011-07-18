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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Navigation {
    class AstScopeNode : IScopeNode {
        private readonly PythonAst _ast;
        private readonly IPythonProjectEntry _projectEntry;
        
        public AstScopeNode(PythonAst pythonAst, IPythonProjectEntry projectEntry) {
            _ast = pythonAst;
            _projectEntry = projectEntry;
        }

        #region IScopeNode Members

        public LibraryNodeType NodeType {
            get {
                return LibraryNodeType.Namespaces;
            }
        }

        public string Name {
            get { return _ast.Name; }
        }

        public string Description {
            get { return _ast.Documentation; }
        }

        public SourceLocation Start {
            get { return _ast.GetStart(_ast); }
        }

        public SourceLocation End {
            get { return _ast.GetEnd(_ast); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get {
                return EnumerateBody(_ast, _ast.Body);
            }
        }

        internal static IEnumerable<IScopeNode> EnumerateBody(PythonAst ast, Statement body, bool includeAssignments = true) {
            SuiteStatement suite = body as SuiteStatement;
            if (suite != null) {
                foreach (Statement stmt in suite.Statements) {
                    ClassDefinition klass = stmt as ClassDefinition;
                    if (klass != null) {
                        yield return new ClassScopeNode(klass);
                        continue;
                    }

                    FunctionDefinition func = stmt as FunctionDefinition;
                    if (func != null) {
                        yield return new FunctionScopeNode(func);
                        continue;
                    }

                    AssignmentStatement assign;
                    if (includeAssignments && (assign = stmt as AssignmentStatement) != null) {
                        foreach (var target in assign.Left) {
                            NameExpression name = target as NameExpression;
                            if (name != null) {
                                yield return new AssignmentScopeNode(ast, assign, name);
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}

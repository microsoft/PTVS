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
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Parsing;

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
            get { return AstScopeNode.FromPythonSourceLocation(_ast.GetStart(_ast)); }
        }

        public SourceLocation End {
            get { return AstScopeNode.FromPythonSourceLocation(_ast.GetEnd(_ast)); }
        }

        public IEnumerable<IScopeNode> NestedScopes {
            get {
                if (_ast == null) {
                    return new IScopeNode[0];
                }
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

        internal static SourceLocation FromPythonSourceLocation(Microsoft.PythonTools.Parsing.SourceLocation sourceLoc) {
            if (sourceLoc.IsValid) {
                return new SourceLocation(
                    sourceLoc.Index,
                    sourceLoc.Line,
                    sourceLoc.Column
                );
            }
            return SourceLocation.Invalid;
        }
        #endregion
    }
}

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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonFunction : IPythonFunction {
        private readonly List<AstPythonFunctionOverload> _overloads;

        public AstPythonFunction(PythonAst ast, IPythonModule declModule, IPythonType declType, FunctionDefinition def, string doc) {
            DeclaringModule = declModule;
            DeclaringType = declType;

            Name = def.Name;
            Documentation = doc;

            foreach (var dec in (def.Decorators?.Decorators).MaybeEnumerate().OfType<NameExpression>()) {
                if (dec.Name == "classmethod") {
                    IsClassMethod = true;
                } else if (dec.Name == "staticmethod") {
                    IsStatic = true;
                }
            }

            _overloads = new List<AstPythonFunctionOverload> {
                new AstPythonFunctionOverload(Documentation, "", MakeParameters(ast, def), MakeReturns(def))
            };
        }

        internal void AddOverload(PythonAst ast, FunctionDefinition def) {
            _overloads.Add(new AstPythonFunctionOverload(def.Documentation, "", MakeParameters(ast, def), MakeReturns(def)));
        }

        private static IEnumerable<IParameterInfo> MakeParameters(PythonAst ast, FunctionDefinition def) {
            foreach (var p in def.Parameters) {
                yield return new AstPythonParameterInfo(ast, p);
            }
        }

        private static IEnumerable<IPythonType> MakeReturns(FunctionDefinition def) {
            yield break;
        }

        public IPythonModule DeclaringModule {get;}
        public IPythonType DeclaringType {get;}
        public string Name { get; }
        public string Documentation {get;}
        public bool IsBuiltin => true;

        public bool IsClassMethod { get; }
        public bool IsStatic { get; }

        public PythonMemberType MemberType => DeclaringType == null ? PythonMemberType.Function : PythonMemberType.Method;

        public IList<IPythonFunctionOverload> Overloads => _overloads.ToArray();
    }
}

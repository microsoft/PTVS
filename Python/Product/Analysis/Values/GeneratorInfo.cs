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

using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a generator instance - either constructed using a generator expression or
    /// by calling a function definition which contains yield expressions.
    /// </summary>
    internal class GeneratorInfo : BuiltinInstanceInfo {
        private AnalysisValue _nextMethod;
        private AnalysisValue _sendMethod;
        private readonly IPythonProjectEntry _declaringModule;
        private readonly int _declaringVersion;
        public readonly VariableDef Yields;
        public readonly VariableDef Sends;
        public readonly VariableDef Returns;

        public GeneratorInfo(PythonAnalyzer projectState, IPythonProjectEntry entry)
            : base(projectState.ClassInfos[BuiltinTypeId.Generator]) {
            
            _declaringModule = entry;
            _declaringVersion = entry.AnalysisVersion;
            Yields = new VariableDef();
            Sends = new VariableDef();
            Returns = new VariableDef();
        }

        public override IPythonProjectEntry DeclaringModule { get { return _declaringModule; } }
        public override int DeclaringVersion { get { return _declaringVersion; } }

        private IAnalysisSet GeneratorNext(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return GetEnumeratorTypes(node, unit);
        }

        private IAnalysisSet GeneratorSend(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length > 0) {
                AddSend(node, unit, args[0]);
            }
            return GetEnumeratorTypes(node, unit);
        }


        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            switch(name) {
                case "next":
                    if (unit.ProjectState.LanguageVersion.Is2x()) {
                        return _nextMethod = _nextMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            GeneratorNext,
                            false
                        );
                    }
                    break;
                case "__next__":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        return _nextMethod = _nextMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            GeneratorNext,
                            false
                        );
                    }
                    break;
                case "send":
                    return _sendMethod = _sendMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        GeneratorSend,
                        false
                    );
            }

            return res;
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            Yields.AddDependency(unit);

            return Yields.Types;
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            base.AddReference(node, analysisUnit);
        }

        public void AddYield(Node node, AnalysisUnit unit, IAnalysisSet yieldValue, bool enqueue = true) {
            Yields.MakeUnionStrongerIfMoreThan(ProjectState.Limits.YieldTypes, yieldValue);
            Yields.AddTypes(unit, yieldValue, enqueue);
        }

        public void AddReturn(Node node, AnalysisUnit unit, IAnalysisSet returnValue, bool enqueue = true) {
            Returns.MakeUnionStrongerIfMoreThan(ProjectState.Limits.ReturnTypes, returnValue);
            Returns.AddTypes(unit, returnValue, enqueue);
        }

        public void AddSend(Node node, AnalysisUnit unit, IAnalysisSet sendValue, bool enqueue = true) {
            Sends.AddTypes(unit, sendValue, enqueue);
        }

        public void AddYieldFrom(Node node, AnalysisUnit unit, IAnalysisSet yieldsFrom, bool enqueue = true) {
            foreach (var ns in yieldsFrom) {
                AddYield(node, unit, ns.GetEnumeratorTypes(node, unit), enqueue);
                var gen = ns as GeneratorInfo;
                if (gen != null) {
                    gen.AddSend(node, unit, Sends.Types, enqueue);
                    AddReturn(node, unit, gen.Returns.Types, enqueue);
                }
            }
        }
    }
}

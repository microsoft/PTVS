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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class TypingModuleInfo : BuiltinModule {
        private readonly BuiltinModule _inner;
        private readonly Dictionary<string, IAnalysisSet> _callables;

        private TypingModuleInfo(BuiltinModule inner)
            : base(inner.InterpreterModule, inner.ProjectState) {
            _inner = inner;
            _callables = new Dictionary<string, IAnalysisSet>();
        }

        public static BuiltinModule Wrap(BuiltinModule inner) => new TypingModuleInfo(inner);

        private IAnalysisSet GetBuiltin(BuiltinTypeId typeId) {
            return ProjectState.ClassInfos[typeId];
        }

        private IAnalysisSet GetFunction(Node node, AnalysisUnit unit, string name, CallDelegate callable) {
            lock (_callables) {
                if (_callables.TryGetValue(name, out var res)) {
                    return res;
                }
            }
            if (unit.ForEval) {
                return null;
            }

            var inner = _inner.GetMember(node, unit, name).OfType<BuiltinFunctionInfo>().FirstOrDefault();
            lock (_callables) {
                return _callables[name] = new SpecializedCallable(inner, callable, false);
            }
        }

        private IAnalysisSet NewType_Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.Scope.GetOrMakeNodeValue(node, Analyzer.NodeValueKind.TypeAnnotation, n => {
                var name = PythonAnalyzer.GetArg(args, keywordArgNames, null, 0).GetConstantValueAsString().FirstOrDefault(x => !string.IsNullOrEmpty(x));
                var baseType = PythonAnalyzer.GetArg(args, keywordArgNames, null, 1) ?? unit.State.ClassInfos[BuiltinTypeId.Object].Instance;
                if (string.IsNullOrEmpty(name)) {
                    return baseType;
                }
                var instPi = new ProtocolInfo(unit.Entry, unit.State);
                var np = new NameProtocol(instPi, name, memberType: PythonMemberType.Instance);
                var cls = new NamespaceProtocol(instPi, "__class__");
                instPi.AddProtocol(np);
                instPi.AddProtocol(cls);

                var pi = new ProtocolInfo(unit.Entry, unit.State);
                pi.AddProtocol(new NameProtocol(pi, name));
                pi.AddProtocol(new InstanceProtocol(pi, Array.Empty<IAnalysisSet>(), instPi));
                pi.AddReference(n, unit);

                cls.SetMember(n, unit, null, pi);

                return pi;
            });
        }

        private IAnalysisSet Import(string moduleName, string typeName, Node node, AnalysisUnit unit) {
            ModuleReference mod;
            if (!ProjectState.Modules.TryImport(moduleName, out mod)) {
                return AnalysisSet.Empty;
            }

            return mod.AnalysisModule.GetMember(node, unit, typeName);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            IAnalysisSet res = null;

            switch (name) {
                case "Any":
                    res = AnalysisSet.Empty;
                    break;

                case "Callable":
                case "Generic":
                case "Optional":
                case "Tuple":
                case "Union":
                case "Container":
                case "ItemsView":
                case "Iterable":
                case "Iterator":
                case "KeysView":
                case "Mapping":
                case "MappingView":
                case "MutableMapping":
                case "MutableSequence":
                case "MutableSet":
                case "Sequence":
                case "ValuesView":
                case "Dict":
                case "List":
                case "Set":
                case "FrozenSet":
                case "NamedTuple":
                case "Generator":
                case "ClassVar":
                    res = new TypingTypeInfo(name, _inner.GetMember(node, unit, name)?.FirstOrDefault());
                    break;

                case "AbstractSet": break;
                case "GenericMeta": break;

                // As our purposes are purely informational, it's okay to
                // "round up" to the nearest type. That said, proper protocol
                // support would be nice to implement.
                case "ContextManager": break;
                case "Hashable": break;
                case "Reversible": break;
                case "SupportsAbs": break;
                case "SupportsBytes": res = GetBuiltin(BuiltinTypeId.Bytes); break;
                case "SupportsComplex": res = GetBuiltin(BuiltinTypeId.Complex); break;
                case "SupportsFloat": res = GetBuiltin(BuiltinTypeId.Float); break;
                case "SupportsInt": res = GetBuiltin(BuiltinTypeId.Int); break;
                case "SupportsRound": break;
                case "Sized": break;

                case "Counter": res = Import("collections", "Counter", node, unit); break;
                case "Deque": res = Import("collections", "deque", node, unit); break;
                case "DefaultDict": res = Import("collections", "defaultdict", node, unit); break;
                case "Type": res = GetBuiltin(BuiltinTypeId.Type); break;
                case "ByteString": res = GetBuiltin(BuiltinTypeId.Bytes); break;
                case "AnyStr": res = GetBuiltin(BuiltinTypeId.Unicode).Union(GetBuiltin(BuiltinTypeId.Bytes), canMutate: false); break;
                case "Text": res = GetBuiltin(BuiltinTypeId.Str); break;

                // TypeVar is not actually a synonym for NewType, but it is close enough for our purposes
                case "TypeVar":
                case "NewType": res = GetFunction(node, unit, name, NewType_Call); break;

                // The following are added depending on presence
                // of their non-generic counterparts in stdlib:
                // Awaitable
                // AsyncIterator
                // AsyncIterable
                // Coroutine
                // Collection
                // AsyncGenerator
                // AsyncContextManager
            }

            return res ?? _inner.GetMember(node, unit, name);
        }
    }
}

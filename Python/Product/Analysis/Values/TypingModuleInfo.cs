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

using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class TypingModuleInfo : BuiltinModule {
        private TypingModuleInfo(BuiltinModule inner)
            : base(inner.InterpreterModule, inner.ProjectState) {
        }

        public static BuiltinModule Wrap(BuiltinModule inner) => new TypingModuleInfo(inner);

        private IAnalysisSet GetBuiltin(BuiltinTypeId typeId) {
            return ProjectState.ClassInfos[typeId];
        }

        private IAnalysisSet Import(string moduleName, string typeName, Node node, AnalysisUnit unit) {
            ModuleReference mod;
            if (!ProjectState.Modules.TryImport(moduleName, out mod)) {
                return AnalysisSet.Empty;
            }

            return mod.AnalysisModule.GetMember(node, unit, typeName);
        }

        public IAnalysisSet GetTypingMember(Node node, AnalysisUnit unit, string name) {
            IAnalysisSet res = null;

            switch (name) {
                case "Any":
                    res = AnalysisSet.Empty;
                    break;

                case "Callable":
                case "Generic":
                case "Optional":
                case "Tuple":
                case "TypeVar":
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
                    res = new TypingTypeInfo(name);
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

            return res;
        }
    }
}

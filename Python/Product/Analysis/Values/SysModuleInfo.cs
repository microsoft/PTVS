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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class SysModuleInfo : BuiltinModule {
        public SysModulesDictionaryInfo _modules;
        
        private SysModuleInfo(BuiltinModule inner)
            : base(inner.InterpreterModule, inner.ProjectState) {
        }

        public static BuiltinModule Wrap(BuiltinModule inner) => new SysModuleInfo(inner);

        public Dictionary<string, IAnalysisSet> Modules { get; private set; }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            if (name == "modules") {
                if (_modules == null) {
                    Modules = new Dictionary<string, IAnalysisSet>();
                    _modules = new SysModulesDictionaryInfo(this, unit.ProjectEntry, node);
                }
                res = _modules;
            }

            return res;
        }


        internal class SysModulesDictionaryInfo : DictionaryInfo {
            private readonly SysModuleInfo _owner;
            private AnalysisValue _getOrPopMethod;

            public SysModulesDictionaryInfo(SysModuleInfo owner, ProjectEntry declaringModule, Node node)
                : base(declaringModule, node) {
                _owner = owner;
            }

            public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
                var res = base.GetIndex(node, unit, index);

                var names = index.GetConstantValueAsString()
                    .Where(s => !string.IsNullOrEmpty(s) && ModulePath.IsImportable(s))
                    .Distinct()
                    .ToArray();

                if (names.Length != 1) {
                    // Unless you request a specific module by string literal,
                    // you won't get any object out of sys.modules.
                    return AnalysisSet.Empty;
                }

                var name = names[0];

                lock (_owner.Modules) {
                    IAnalysisSet knownValues;
                    if (_owner.Modules.TryGetValue(name, out knownValues) &&
                        knownValues != null &&
                        knownValues.Any()
                    ) {
                        return knownValues;
                    }
                }

                ModuleReference modRef;
                if (unit.State.Modules.TryImport(name, out modRef)) {
                    return modRef.AnalysisModule;
                }

                return AnalysisSet.Empty;
            }

            public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
                base.SetIndex(node, unit, index, value);

                foreach (var name in index.OfType<ConstantInfo>()
                    .Select(ci => ci.GetConstantValueAsString())
                    .Where(s => !string.IsNullOrEmpty(s))
                ) {
                    lock (_owner.Modules) {
                        _owner.Modules[name] = value;
                    }

                    var modules = value.OfType<ModuleInfo>().ToArray();

                    var modRef = unit.State.Modules.GetOrAdd(name);

                    MultipleMemberInfo mmi;
                    ModuleInfo mi;
                    if ((mmi = modRef.Module as MultipleMemberInfo) != null) {
                        if (modules.Except(mmi.Members.OfType<ModuleInfo>()).Any()) {
                            modules = modules.Concat(mmi.Members.OfType<ModuleInfo>()).Distinct().ToArray();
                        }
                    } else if ((mi = modRef.Module as ModuleInfo) != null) {
                        if (!modules.Contains(mi)) {
                            modules = modules.Concat(Enumerable.Repeat(mi, 1)).ToArray();
                        }
                    }
                    modRef.Module = MultipleMemberInfo.Create(modules) as IModule;

                    foreach (var module in modules) {
                        int lastDot = name.LastIndexOf('.');
                        if (lastDot > 0) {
                            var parentName = name.Remove(lastDot);
                            ModuleReference parent;
                            if (ProjectState.Modules.TryImport(parentName, out parent)) {
                                if ((mi = parent.AnalysisModule as ModuleInfo) != null) {
                                    mi.AddChildPackage(module, unit, name.Substring(lastDot + 1));
                                }
                            }
                        }
                    }
                }
            }

            public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
                // Must unconditionally call the base implementation of GetMember
                var res = base.GetMember(node, unit, name);

                switch (name) {
                    case "get":
                    case "pop":
                        return _getOrPopMethod = _getOrPopMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            DictionaryGetOrPop,
                            false
                        );
                }

                return res;
            }

            private IAnalysisSet DictionaryGetOrPop(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                if (args.Length >= 1) {
                    var res = GetIndex(node, unit, args[0]);
                    if (!res.Any() && args.Length >= 2) {
                        res = args[1];
                    }
                    return res;
                }
                return AnalysisSet.Empty;
            }

        }
    }
}

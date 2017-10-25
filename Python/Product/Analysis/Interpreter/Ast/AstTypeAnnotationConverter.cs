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

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstTypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly NameLookupContext _scope;

        public AstTypeAnnotationConverter(NameLookupContext scope) {
            _scope = scope;
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type is ModuleType) {
                return null;
            }

            return type;
        }

        public override IPythonType LookupName(string name) {
            var m = _scope.LookupNameInScopes(name, NameLookupContext.LookupOptions.Global | NameLookupContext.LookupOptions.Builtins);
            if (m is IPythonMultipleMembers mm) {
                m = mm.Members.OfType<IPythonType>().FirstOrDefault<IMember>() ??
                    mm.Members.OfType<IPythonModule>().FirstOrDefault();
            }
            if (m is IPythonModule mod) {
                // Wrap the module in an IPythonType interface
                return new ModuleType(mod);
            }
            return m as IPythonType;
        }

        public override IPythonType GetTypeMember(IPythonType baseType, string member) {
            return baseType.GetMember(_scope.Context, member) as IPythonType;
        }

        public override IPythonType MakeUnion(IReadOnlyList<IPythonType> types) {
            return new UnionType(types);
        }

        public override IReadOnlyList<IPythonType> GetUnionTypes(IPythonType unionType) {
            return (unionType as UnionType)?.Types ??
                   (unionType as IPythonMultipleMembers)?.Members.OfType<IPythonType>().ToArray();
        }

        private class ModuleType : IPythonType {
            public ModuleType(IPythonModule module) {
                DeclaringModule = module;
            }

            public IPythonModule DeclaringModule { get; }

            public string Name => DeclaringModule.Name;
            public string Documentation => DeclaringModule.Documentation;
            public BuiltinTypeId TypeId => BuiltinTypeId.Module;
            public IList<IPythonType> Mro => new[] { this };
            public bool IsBuiltin => true;
            public PythonMemberType MemberType => PythonMemberType.Module;
            public IPythonFunction GetConstructors() => null;

            public IMember GetMember(IModuleContext context, string name) => DeclaringModule.GetMember(context, name);
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => DeclaringModule.GetMemberNames(moduleContext);
        }

        private class UnionType : IPythonMultipleMembers, IPythonType {
            public UnionType(IReadOnlyList<IPythonType> types) {
                Types = types;
            }

            public IReadOnlyList<IPythonType> Types { get; }

            public IList<IMember> Members => Types.OfType<IMember>().ToArray();

            public PythonMemberType MemberType => PythonMemberType.Unknown;
            public string Name => "Any";
            public string Documentation => null;
            public BuiltinTypeId TypeId => BuiltinTypeId.Unknown;
            public IPythonModule DeclaringModule => null;
            public IList<IPythonType> Mro => null;
            public bool IsBuiltin => false;
            public IPythonFunction GetConstructors() => null;

            public IMember GetMember(IModuleContext context, string name) => new UnionType(
                Types.Select(t => t.GetMember(context, name)).OfType<IPythonType>().ToArray()
            );

            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Types.SelectMany(t => t.GetMemberNames(moduleContext));
        }
    }
}

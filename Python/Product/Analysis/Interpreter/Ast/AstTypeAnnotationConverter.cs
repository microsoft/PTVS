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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstTypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly NameLookupContext _scope;

        public AstTypeAnnotationConverter(NameLookupContext scope) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        /// <summary>
        /// Soft-casts a member to a type, extracting the type from
        /// a multi-member object if possible.
        /// </summary>
        private static IPythonType AsIPythonType(IMember m) {
            if (m is IPythonMultipleMembers mm) {
                return new AstPythonMultipleTypes(mm.Members.OfType<IPythonType>()).Trim();
            }
            return m as IPythonType;
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type == null || type is ModuleType) {
                return null;
            }

            if (type == _scope._unknownType) {
                return null;
            }

            var n = GetName(type);
            if (!string.IsNullOrEmpty(n)) {
                return AsIPythonType(_scope.LookupNameInScopes(n));
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
            return AsIPythonType(baseType.GetMember(_scope.Context, member));
        }

        public override IPythonType MakeNameType(string name) => new NameType(name);
        public override string GetName(IPythonType type) => (type as NameType)?.Name;

        public override IPythonType MakeUnion(IReadOnlyList<IPythonType> types) {
            return new UnionType(types);
        }

        public override IReadOnlyList<IPythonType> GetUnionTypes(IPythonType unionType) {
            return (unionType as UnionType)?.Types ??
                   (unionType as IPythonMultipleMembers)?.Members.OfType<IPythonType>().ToArray();
        }

        public override IPythonType MakeGeneric(IPythonType baseType, IReadOnlyList<IPythonType> args) {
            if (args == null || args.Count == 0 || baseType == null) {
                return baseType;
            }
            if (baseType.DeclaringModule?.Name != "typing" && !(baseType is NameType) && !(baseType is UnionType) && !(baseType is ModuleType)) {
                return baseType;
            }

            switch (baseType.Name) {
                case "Tuple":
                    return MakeSequenceType(BuiltinTypeId.Tuple, args);
                case "List":
                    return MakeSequenceType(BuiltinTypeId.List, args);
                case "Set":
                    return MakeSequenceType(BuiltinTypeId.Set, args);
                case "Optional":
                    return Finalize(args.FirstOrDefault()) ?? _scope._unknownType;
                case "Union":
                    return MakeUnion(args);
                case "Type":
                    return _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Type);
                // TODO: Other types
                default:
                    Trace.TraceWarning("Unhandled generic: typing.{0}", baseType.Name);
                    break;
            }

            return baseType;
        }

        private IPythonType MakeSequenceType(BuiltinTypeId typeId, IReadOnlyList<IPythonType> types) {
            var res = _scope.Interpreter.GetBuiltinType(typeId);
            if (types.Count > 0) {
                res = new AstPythonSequence(res, _scope.Module, types.Select(Finalize));
            }
            return res;
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

        private class NameType : IPythonType {
            public NameType(string name) {
                Name = name;
            }

            public IPythonModule DeclaringModule => null;

            public string Name { get; }
            public string Documentation => null;
            public BuiltinTypeId TypeId => BuiltinTypeId.Unknown;
            public IList<IPythonType> Mro => null;
            public bool IsBuiltin => true;
            public PythonMemberType MemberType => PythonMemberType.Unknown;
            public IPythonFunction GetConstructors() => null;

            public IMember GetMember(IModuleContext context, string name) => null;
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => null;
        }
    }
}

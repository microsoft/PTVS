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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis {
    internal interface IKnownPythonTypes {
        IPythonType this[BuiltinTypeId id] { get; }
    }

    internal interface IKnownClasses {
        BuiltinClassInfo this[BuiltinTypeId id] { get; }
    }

    internal class KnownTypes : IKnownPythonTypes, IKnownClasses {
        internal readonly IPythonType[] _types;
        internal readonly BuiltinClassInfo[] _classInfos;

        public static KnownTypes CreateDefault(PythonAnalyzer state, IBuiltinPythonModule fallback) {
            var res = new KnownTypes();

            for (int value = 0; value < res._types.Length; ++value) {
                res._types[value] = (IPythonType)fallback.GetAnyMember(
                    ((BuiltinTypeId)value).GetTypeName(state.LanguageVersion)
                );
                Debug.Assert(res._types[value] != null);
            }

            res.SetClassInfo(state);
            return res;
        }

        public static KnownTypes Create(PythonAnalyzer state, IBuiltinPythonModule fallback) {
            var res = new KnownTypes();

            var interpreter = state.Interpreter;

            for (int value = 0; value < res._types.Length; ++value) {
                IPythonType type;
                try {
                    type = interpreter.GetBuiltinType((BuiltinTypeId)value);
                } catch (KeyNotFoundException) {
                    type = null;
                }
                if (type == null) {
                    type = (IPythonType)fallback.GetAnyMember(((BuiltinTypeId)value).GetTypeName(state.LanguageVersion));
                    Debug.Assert(type != null);
                }
                res._types[value] = type;
            }

            res.SetClassInfo(state);
            return res;
        }

        private KnownTypes() {
            int count = (int)BuiltinTypeIdExtensions.LastTypeId + 1;
            _types = new IPythonType[count];
            _classInfos = new BuiltinClassInfo[count];
        }

        private void SetClassInfo(PythonAnalyzer state) {
            for (int value = 0; value < _types.Length; ++value) {
                if (_types[value] != null) {
                    _classInfos[value] = state.GetBuiltinType(_types[value]);
                }
            }
        }

        IPythonType IKnownPythonTypes.this[BuiltinTypeId id] {
            get {
                return _types[(int)id];
            }
        }

        BuiltinClassInfo IKnownClasses.this[BuiltinTypeId id] {
            get {
                return _classInfos[(int)id];
            }
        }
    }

    class FallbackBuiltinModule : IBuiltinPythonModule, IPythonModule {
        public readonly PythonLanguageVersion LanguageVersion;
        private readonly Dictionary<BuiltinTypeId, IMember> _cachedInstances;

        public FallbackBuiltinModule(PythonLanguageVersion version) {
            LanguageVersion = version;
            _cachedInstances = new Dictionary<BuiltinTypeId, IMember>();
            Name = BuiltinTypeId.Unknown.GetModuleName(version);
        }

        private IMember GetOrCreate(BuiltinTypeId typeId) {
            if (typeId.IsVirtualId()) {
                switch (typeId) {
                    case BuiltinTypeId.Str:
                        typeId = LanguageVersion.Is3x() ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
                        break;
                    case BuiltinTypeId.StrIterator:
                        typeId = LanguageVersion.Is3x() ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;
                        break;
                    default:
                        typeId = BuiltinTypeId.Unknown;
                        break;
                }
            }

            lock (_cachedInstances) {
                if (!_cachedInstances.TryGetValue(typeId, out var value)) {
                    _cachedInstances[typeId] = value = new FallbackBuiltinPythonType(this, typeId);
                }
                return value;
            }
        }

        public string Documentation => string.Empty;
        public PythonMemberType MemberType => PythonMemberType.Module;
        public string Name { get; }

        public IMember GetAnyMember(string name) {
            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                if (typeId.GetTypeName(LanguageVersion) == name) {
                    return GetOrCreate(typeId);
                }
            }
            return GetOrCreate(BuiltinTypeId.Unknown);
        }

        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();
        public IMember GetMember(IModuleContext context, string name) => null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
        public void Imported(IModuleContext context) { }
    }

    class FallbackBuiltinPythonType : IPythonType {
        public FallbackBuiltinPythonType(IBuiltinPythonModule module, BuiltinTypeId typeId, string name = null) {
            DeclaringModule = module;
            Name = name ?? typeId.GetModuleName((DeclaringModule as FallbackBuiltinModule)?.LanguageVersion ?? PythonLanguageVersion.None);
            TypeId = typeId;
        }

        public IPythonModule DeclaringModule { get; }
        public string Documentation => string.Empty;
        public bool IsBuiltin => true;
        public PythonMemberType MemberType => PythonMemberType.Class;
        public IList<IPythonType> Mro => new[] { (IPythonType)this };
        public string Name { get; }
        public BuiltinTypeId TypeId {get;}
        public IPythonFunction GetConstructors() => null;
        public IMember GetMember(IModuleContext context, string name) => null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
    }
}

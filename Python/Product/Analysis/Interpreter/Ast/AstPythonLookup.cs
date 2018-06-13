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

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonLookup : IPythonLookupType, IPythonIterableType {
        private readonly IPythonType _lookupType;
        private readonly IReadOnlyDictionary<IPythonType, IReadOnlyList<IPythonType>> _mapping;

        public AstPythonLookup(
            IPythonType lookupType,
            IPythonModule declaringModule,
            IEnumerable<IPythonType> keys,
            IEnumerable<IPythonType> values,
            IEnumerable<KeyValuePair<IPythonType, IEnumerable<IPythonType>>> mapping,
            IPythonIteratorType iterator
        ) {
            _lookupType = lookupType;
            KeyTypes = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();
            ValueTypes = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
            _mapping = mapping?.ToDictionary(k => k.Key, k => (IReadOnlyList<IPythonType>)k.Value.ToArray());
            DeclaringModule = declaringModule;
            IteratorType = iterator;
        }

        public IEnumerable<IPythonType> KeyTypes { get; }
        public IEnumerable<IPythonType> ValueTypes { get; }
        public IEnumerable<IPythonType> GetIndex(IPythonType key) {
            if (_mapping != null && _mapping.TryGetValue(key, out var res)) {
                return res;
            }
            return Enumerable.Empty<IPythonType>();
        }

        public IPythonIteratorType IteratorType { get; }

        public IPythonModule DeclaringModule { get; }

        public string Name => _lookupType?.Name ?? "tuple";
        public string Documentation => _lookupType?.Documentation ?? string.Empty;
        public BuiltinTypeId TypeId => _lookupType?.TypeId ?? BuiltinTypeId.Tuple;
        public IList<IPythonType> Mro => _lookupType?.Mro ?? Array.Empty<IPythonType>();
        public bool IsBuiltin => _lookupType?.IsBuiltin ?? true;
        public PythonMemberType MemberType => _lookupType?.MemberType ?? PythonMemberType.Class;
        public IPythonFunction GetConstructors() => _lookupType?.GetConstructors();
        public IMember GetMember(IModuleContext context, string name) => _lookupType?.GetMember(context, name) ?? null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => _lookupType?.GetMemberNames(moduleContext) ?? Enumerable.Empty<string>();
    }
}

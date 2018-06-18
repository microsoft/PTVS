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
    class AstPythonSequence : IPythonSequenceType, IPythonIterableType {
        private readonly IPythonType _sequenceType;

        public AstPythonSequence(
            IPythonType sequenceType,
            IPythonModule declaringModule,
            IEnumerable<IPythonType> contents,
            IPythonType iteratorBase
        ) {
            _sequenceType = sequenceType;
            IndexTypes = (contents ?? throw new ArgumentNullException(nameof(contents))).ToArray();
            DeclaringModule = declaringModule;
            IteratorType = new AstPythonIterator(iteratorBase, IndexTypes, declaringModule);
        }

        public IEnumerable<IPythonType> IndexTypes { get; }
        public IPythonIteratorType IteratorType { get; }
        public IPythonModule DeclaringModule { get; }

        public string Name => _sequenceType?.Name ?? "tuple";
        public string Documentation => _sequenceType?.Documentation ?? string.Empty;
        public BuiltinTypeId TypeId => _sequenceType?.TypeId ?? BuiltinTypeId.Tuple;
        public IList<IPythonType> Mro => _sequenceType?.Mro ?? Array.Empty<IPythonType>();
        public bool IsBuiltin => _sequenceType?.IsBuiltin ?? true;
        public PythonMemberType MemberType => _sequenceType?.MemberType ?? PythonMemberType.Class;
        public IPythonFunction GetConstructors() => _sequenceType?.GetConstructors();
        public IMember GetMember(IModuleContext context, string name) => _sequenceType?.GetMember(context, name) ?? null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => _sequenceType?.GetMemberNames(moduleContext) ?? Enumerable.Empty<string>();
    }
}

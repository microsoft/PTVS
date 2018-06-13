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

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonIterable : IPythonIterableType {
        private readonly IPythonType _type;

        public AstPythonIterable(
            IPythonType iterableType,
            IEnumerable<IPythonType> types,
            IPythonType iteratorBase,
            IPythonModule declModule
        ) {
            _type = iterableType;
            IteratorType = new AstPythonIterator(iteratorBase, types, declModule);
            DeclaringModule = declModule;
        }

        public AstPythonIterable(
            IPythonType iterableType,
            IEnumerable<IPythonType> types,
            IPythonIteratorType iteratorType,
            IPythonModule declModule
        ) {
            _type = iterableType;
            IteratorType = iteratorType;
            DeclaringModule = declModule;
        }

        public IPythonIteratorType IteratorType { get; }
        public IPythonModule DeclaringModule { get; }

        public string Name => _type.Name;
        public string Documentation => _type.Documentation;
        public BuiltinTypeId TypeId => _type.TypeId;
        public IList<IPythonType> Mro => _type.Mro;
        public bool IsBuiltin => _type.IsBuiltin;
        public PythonMemberType MemberType => _type.MemberType;
        public IPythonFunction GetConstructors() => _type.GetConstructors();
        public IMember GetMember(IModuleContext context, string name) => _type.GetMember(context, name);
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => _type.GetMemberNames(moduleContext);
    }

    class AstPythonIterator : IPythonIteratorType, IPythonIterableType {
        private readonly IPythonType _type;

        public AstPythonIterator(IPythonType iterableType, IEnumerable<IPythonType> nextType, IPythonModule declModule) {
            _type = iterableType;
            NextType = nextType.ToArray();
            DeclaringModule = declModule;
        }

        public IPythonIteratorType IteratorType => this;
        public IEnumerable<IPythonType> NextType { get; }
        public IPythonModule DeclaringModule { get; }

        public string Name => _type.Name;
        public string Documentation => _type.Documentation;
        public BuiltinTypeId TypeId => _type.TypeId;
        public IList<IPythonType> Mro => _type.Mro;
        public bool IsBuiltin => _type.IsBuiltin;
        public PythonMemberType MemberType => _type.MemberType;
        public IPythonFunction GetConstructors() => _type.GetConstructors();
        public IMember GetMember(IModuleContext context, string name) => _type.GetMember(context, name);
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => _type.GetMemberNames(moduleContext);
    }
}

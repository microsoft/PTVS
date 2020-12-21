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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonType : PythonObject, IAdvancedPythonType {
        private IPythonFunction _ctors;
        private string _name;
        private BuiltinTypeId? _typeId;
        private IList<IPythonType> _propagateOnCall;
        private IReadOnlyList<IPythonType> _mro;
        private PythonMemberType _memberType;
        private bool? _genericTypeDefinition;

        internal static IPythonType[] EmptyTypes = new IPythonType[0];

        public IronPythonType(IronPythonInterpreter interpreter, ObjectIdentityHandle type)
            : base(interpreter, type) {
        }

        #region IPythonType Members

        public IPythonFunction GetConstructors() {
            if (_ctors == null) {
                var ri = RemoteInterpreter;
                if (ri != null && !ri.PythonTypeHasNewOrInitMethods(Value)) {
                    _ctors = GetClrOverloads();
                }

                if (_ctors == null) {
                    _ctors = GetMember(null, "__new__") as IPythonFunction;
                }
            }
            return _ctors;
        }

        /// <summary>
        /// Returns the overloads for a normal .NET type
        /// </summary>
        private IPythonFunction GetClrOverloads() {
            var ri = RemoteInterpreter;
            var ctors = ri != null ? ri.GetPythonTypeConstructors(Value) : null;
            if (ctors != null) {
                return new IronPythonConstructorFunction(Interpreter, ctors, this);
            }
            return null;
        }

        public override PythonMemberType MemberType {
            get {
                if (_memberType == PythonMemberType.Unknown) {
                    var ri = RemoteInterpreter;
                    _memberType = ri != null ? ri.GetPythonTypeMemberType(Value) : PythonMemberType.Unknown;
                }
                return _memberType;
            }
        }

        public string Name {
            get {
                if (_name == null) {
                    var ri = RemoteInterpreter;
                    _name = ri != null ? ri.GetPythonTypeName(Value) : string.Empty;
                }

                return _name;
            }
        }

        public string Documentation {
            get {
                var ri = RemoteInterpreter;
                return ri != null ? ri.GetPythonTypeDocumentation(Value) : string.Empty;
            }
        }

        public BuiltinTypeId TypeId {
            get {
                if (_typeId == null) {
                    var ri = RemoteInterpreter;
                    _typeId = ri?.PythonTypeGetBuiltinTypeId(Value) ?? BuiltinTypeId.Unknown;
                }
                return _typeId.Value;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                var ri = RemoteInterpreter;
                return ri != null ? Interpreter.ImportModule(ri.GetTypeDeclaringModule(Value)) : null;
            }
        }

        public IReadOnlyList<IPythonType> Mro {
            get {
                if (_mro == null) {
                    var ri = RemoteInterpreter;
                    var types = ri != null ? ri.GetPythonTypeMro(Value) : new ObjectIdentityHandle[0];
                    var mro = new IPythonType[types.Length];
                    for (int i = 0; i < types.Length; ++i) {
                        mro[i] = Interpreter.GetTypeFromType(types[i]);
                    }
                    _mro = mro;
                }
                return _mro;
            }
        }

        public bool IsBuiltin {
            get {
                return true;
            }
        }

        public IEnumerable<IPythonType> IndexTypes {
            get {
                return null;
            }
        }

        public bool IsArray {
            get {
                var ri = RemoteInterpreter;
                return ri != null ? ri.IsPythonTypeArray(Value) : false;
            }
        }

        public IPythonType GetElementType() {
            var ri = RemoteInterpreter;
            return ri != null ? Interpreter.GetTypeFromType(ri.GetPythonTypeElementType(Value)) : null;
        }

        public IList<IPythonType> GetTypesPropagatedOnCall() {
            if (_propagateOnCall == null) {
                var ri = RemoteInterpreter;
                if (ri != null && ri.IsDelegateType(Value)) {
                    _propagateOnCall = GetEventInvokeArgs();
                } else {
                    _propagateOnCall = EmptyTypes;
                }
            }

            return _propagateOnCall == EmptyTypes ? null : _propagateOnCall;
        }

        #endregion

        private IPythonType[] GetEventInvokeArgs() {
            var ri = RemoteInterpreter;
            var types = ri != null ? ri.GetEventInvokeArgs(Value) : new ObjectIdentityHandle[0];

            var args = new IPythonType[types.Length];
            for (int i = 0; i < types.Length; i++) {
                args[i] = Interpreter.GetTypeFromType(types[i]);
            }
            return args;
        }

        #region IPythonType Members

        public bool IsGenericTypeDefinition {
            get {
                if (_genericTypeDefinition == null) {
                    var ri = RemoteInterpreter;
                    _genericTypeDefinition = ri != null ? ri.IsPythonTypeGenericTypeDefinition(Value) : false;
                }
                return _genericTypeDefinition.Value;
            }
        }

        public IPythonType MakeGenericType(IPythonType[] indexTypes) {
            // Should we hash on the types here?
            ObjectIdentityHandle[] types = new ObjectIdentityHandle[indexTypes.Length];
            for (int i = 0; i < types.Length; i++) {
                types[i] = ((IronPythonType)indexTypes[i]).Value;
            }
            var ri = RemoteInterpreter;
            return ri != null ? Interpreter.GetTypeFromType(ri.PythonTypeMakeGenericType(Value, types)) : null;
        }

        #endregion
    }
}

/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonType : PythonObject, IAdvancedPythonType {
        private IPythonFunction _ctors;
        private string _doc, _name;
        private BuiltinTypeId? _typeId;
        private IList<IPythonType> _propagateOnCall;
        private PythonMemberType _memberType;
        private bool? _genericTypeDefinition;

        internal static IPythonType[] NoPropagateOnCall = new IPythonType[0];

        public IronPythonType(IronPythonInterpreter interpreter, ObjectIdentityHandle type)
            : base(interpreter, type) {
            Debug.Assert(Interpreter.Remote.TypeIs<PythonType>(type));
        }

        #region IPythonType Members

        public IPythonFunction GetConstructors() {
            if (_ctors == null) {
                if (!Interpreter.Remote.PythonTypeHasNewOrInitMethods(Value)) {
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
            var ctors = Interpreter.Remote.GetPythonTypeConstructors(Value);
            if (ctors != null) {
                return new IronPythonConstructorFunction(Interpreter, ctors, this);
            }
            return null;
        }

        public override PythonMemberType MemberType {
            get {
                if (_memberType == PythonMemberType.Unknown) {
                    _memberType = Interpreter.Remote.GetPythonTypeMemberType(Value);
                }
                return _memberType;
            }
        }

        public string Name {
            get {
                if (_name == null) {
                    _name = Interpreter.Remote.GetPythonTypeName(Value);
                }

                return _name; 
            }
        }

        public string Documentation {
            get {
                return Interpreter.Remote.GetPythonTypeDocumentation(Value);
            }
        }

        public BuiltinTypeId TypeId {
            get {
                if (_typeId == null) {
                    _typeId = Interpreter.Remote.PythonTypeGetBuiltinTypeId(Value);
                }
                return _typeId.Value;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                return Interpreter.ImportModule(Interpreter.Remote.GetTypeDeclaringModule(Value));
            }
        }

        public bool IsBuiltin {
            get {
                return true;
            }
        }

        public bool IsArray {
            get {
                return Interpreter.Remote.IsPythonTypeArray(Value);
            }
        }

        public IPythonType GetElementType() {
            return Interpreter.GetTypeFromType(Interpreter.Remote.GetPythonTypeElementType(Value));
        }

        public IList<IPythonType> GetTypesPropagatedOnCall() {
            if (_propagateOnCall == null) {
                if (Interpreter.Remote.IsDelegateType(Value)) {
                    _propagateOnCall = GetEventInvokeArgs();
                } else {
                    _propagateOnCall = NoPropagateOnCall;
                }
            }

            return _propagateOnCall == NoPropagateOnCall ? null : _propagateOnCall;
        }

        #endregion

        private IPythonType[] GetEventInvokeArgs() {
            var types = Interpreter.Remote.GetEventInvokeArgs(Value);

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
                    _genericTypeDefinition = Interpreter.Remote.IsPythonTypeGenericTypeDefinition(Value);
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
            return Interpreter.GetTypeFromType(Interpreter.Remote.PythonTypeMakeGenericType(Value, types));
        }

        #endregion
    }
}

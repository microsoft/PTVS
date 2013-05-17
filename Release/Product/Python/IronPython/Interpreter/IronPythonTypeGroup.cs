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
using System.Linq;
using System.Runtime.Remoting;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonTypeGroup : PythonObject, IAdvancedPythonType {
        private bool? _genericTypeDefinition;
        private PythonMemberType _memberType;
        private IList<IPythonType> _eventInvokeArgs;
        private IPythonFunction _ctors;

        public IronPythonTypeGroup(IronPythonInterpreter interpreter, ObjectIdentityHandle type)
            : base(interpreter, type) {
        }

        #region IPythonType Members

        public IPythonFunction GetConstructors() {
            if (_ctors == null) {
                if (!Interpreter.Remote.TypeGroupHasNewOrInitMethods(Value)) {
                    _ctors = GetClrOverloads();
                }

                if (_ctors == null) {
                    _ctors = GetMember(null, "__new__") as IPythonFunction;
                }
            }
            return _ctors;
        }

        public bool IsPythonType {
            get {
                return false;
            }
        }

        /// <summary>
        /// Returns the overloads for a normal .NET type
        /// </summary>
        private IPythonFunction GetClrOverloads() {
            ObjectIdentityHandle declType;
            var ctors = Interpreter.Remote.GetTypeGroupConstructors(Value, out declType);
            if (ctors != null) {
                return new IronPythonConstructorFunction(Interpreter, ctors, Interpreter.GetTypeFromType(declType));
            }
            return null;            
        }

        public override PythonMemberType MemberType {
            get {
                if (_memberType == PythonMemberType.Unknown) {
                    _memberType = Interpreter.Remote.GetTypeGroupMemberType(Value);
                }
                return _memberType;
            }
        }

        public string Name {
            get { return Interpreter.Remote.GetTypeGroupName(Value); }
        }

        public string Documentation {
            get {
                return Interpreter.Remote.GetTypeGroupDocumentation(Value);
            }
        }

        public BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Unknown;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                return Interpreter.ImportModule(
                    Interpreter.Remote.GetTypeGroupDeclaringModule(Value)
                );
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
                return false;
            }
        }

        public IPythonType GetElementType() {
            return null;
        }

        public IList<IPythonType> GetTypesPropagatedOnCall() {
            if (_eventInvokeArgs == null) {
                var types = Interpreter.Remote.GetTypeGroupEventInvokeArgs(Value);
                if (types == null) {
                    _eventInvokeArgs = IronPythonType.NoPropagateOnCall;
                } else {
                    var args = new IPythonType[types.Length];
                    for (int i = 0; i < types.Length; i++) {
                        args[i] = Interpreter.GetTypeFromType(types[i]);
                    }
                    _eventInvokeArgs = args;
                }
            }

            return _eventInvokeArgs == IronPythonType.NoPropagateOnCall ? null : _eventInvokeArgs;
        }

        #endregion

        #region IPythonType Members

        public bool IsGenericTypeDefinition {
            get {
                if (_genericTypeDefinition == null) {
                    _genericTypeDefinition = Interpreter.Remote.TypeGroupIsGenericTypeDefinition(Value);
                }
                return _genericTypeDefinition.Value;
            }
        }

        public IPythonType MakeGenericType(IPythonType[] indexTypes) {
            // TODO: Caching?
            ObjectIdentityHandle[] types = new ObjectIdentityHandle[indexTypes.Length];
            for (int i = 0; i < types.Length; i++) {
                types[i] = ((IronPythonType)indexTypes[i]).Value;
            }

            return Interpreter.GetTypeFromType(Interpreter.Remote.TypeGroupMakeGenericType(Value, types));
        }

        #endregion
    }
}

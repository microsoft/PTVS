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

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using IronPython.Runtime;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Scripting.Actions;
using System.Text;
using System.Linq;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonTypeGroup : PythonObject<TypeGroup>, IAdvancedPythonType {
        public IronPythonTypeGroup(IronPythonInterpreter interpreter, TypeGroup type)
            : base(interpreter, type) {
        }

        #region IPythonType Members

        public IPythonFunction GetConstructors() {
            foreach (var type in Value.Types) {
                var clrType = ClrModule.GetClrType(type);
                var newMethods = clrType.GetMember("__new__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static);
                if (!IsPythonType) {
                    var initMethods = clrType.GetMember("__init__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance);
                    if (newMethods.Length == 0 && initMethods.Length == 0) {
                        return GetClrOverloads();
                    }
                } else if (clrType == typeof(object)) {
                    return GetClrOverloads();
                }

                return GetMember(null, "__new__") as IPythonFunction;
            }
            return null;
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
            foreach (var clrType in Value.Types) {
                // just a normal .NET type...
                var ctors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance);
                if (ctors.Length > 0) {
                    return new IronPythonConstructorFunction(Interpreter, ctors, Interpreter.GetTypeFromType(clrType));
                }
            }
            return null;            
        }

        public override PythonMemberType MemberType {
            get {
                foreach (var type in Value.Types) {
                    if (type.IsEnum) {
                        return PythonMemberType.Enum;
                    } else if (typeof(Delegate).IsAssignableFrom(type)) {
                        return PythonMemberType.Delegate;
                    }
                }
                return PythonMemberType.Class;
            }
        }

        public string Name {
            get { return Value.Name; }
        }

        public string Documentation {
            get {
                StringBuilder res = new StringBuilder();
                foreach (var type in Value.Types) {
                    res.Append(PythonType.Get__doc__(Interpreter.CodeContext, DynamicHelpers.GetPythonTypeFromType(type)) as string);
                }
                return res.ToString();
            }
        }

        public BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Unknown;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                return this.Interpreter.ImportModule(
                    (string)PythonType.Get__module__(
                        Interpreter.CodeContext, 
                        DynamicHelpers.GetPythonTypeFromType(Value.Types.First())
                    )
                );
            }
        }

        public bool IsBuiltin {
            get {
                return true;
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
            foreach (var type in Value.Types) {
                if (typeof(Delegate).IsAssignableFrom(type)) {
                    return GetEventInvokeArgs(type);
                }
            }
            return null;
        }

        #endregion

        private IPythonType[] GetEventInvokeArgs(Type type) {
            var p = type.GetMethod("Invoke").GetParameters();

            var args = new IPythonType[p.Length];
            for (int i = 0; i < p.Length; i++) {
                args[i] = this.Interpreter.GetTypeFromType(p[i].ParameterType);
            }
            return args;
        }


        #region IPythonType Members


        public bool IsGenericTypeDefinition {
            get {
                foreach (var type in Value.Types) {
                    if (type.IsGenericTypeDefinition) {
                        return true;
                    }
                }
                return false;
            }
        }

        public IPythonType MakeGenericType(IPythonType[] indexTypes) {
            var genType = Value.GetTypeForArity(indexTypes.Length);
            if (genType != null) {

                Type[] types = new Type[indexTypes.Length];
                for (int i = 0; i < types.Length; i++) {
                    types[i] = ((IronPythonType)indexTypes[i]).Value.__clrtype__();
                }
                return Interpreter.GetTypeFromType(genType.Type.MakeGenericType(types));
            }
            return null;
        }

        #endregion
    }
}

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

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonType : PythonObject<PythonType>, IAdvancedPythonType {
        public IronPythonType(IronPythonInterpreter interpreter, PythonType type)
            : base(interpreter, type) {
        }

        #region IPythonType Members

        public IPythonFunction GetConstructors() {
            var clrType = ClrModule.GetClrType(Value);
            var newMethods = clrType.GetMember("__new__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static);
            if (!IsPythonType) {
                var initMethods = clrType.GetMember("__init__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance);
                if (newMethods.Length == 0 && initMethods.Length == 0) {
                    return GetClrOverloads();
                }
            } else if (clrType == typeof(object)) {
                return GetClrOverloads();
            }

            return null;/*
            object ctor;
            if (!ProjectState.TryGetMember(Value, "__new__", out ctor)) {
                ctor = null;
            }
            var func = ctor as IBuiltinFunction;
            if (func == null) {
                return new OverloadResult[0];
            }*/
        }

        public bool IsPythonType {
            get {
                return Value == TypeCache.String ||
                    Value == TypeCache.Object ||
                    Value == TypeCache.Double ||
                    Value == TypeCache.Complex ||
                    Value == TypeCache.Boolean;
            }
        }

        /// <summary>
        /// Returns the overloads for a normal .NET type
        /// </summary>
        private IPythonFunction GetClrOverloads() {
            Type clrType = ClrModule.GetClrType(Value);
            // just a normal .NET type...
            var ctors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance);
            if (ctors.Length > 0) {
                return new IronPythonConstructorFunction(Interpreter, ctors, this);
            }
            return null;
            /*
                        var overloads = new OverloadResult[ctors.Length];
                        for (int i = 0; i < ctors.Length; i++) {
                            // TODO: Docs, python type name
                            var parameters = ctors[i].GetParameters();

                            bool hasContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(CodeContext);

                            var paramResult = new ParameterResult[hasContext ? parameters.Length - 1 : parameters.Length];

                            for (int j = 0; j < paramResult.Length; j++) {
                                var curParam = parameters[j + (hasContext ? 1 : 0)];
                                // TODO: Docs
                                paramResult[j] = BuiltinFunctionOverloadResult.GetParameterResultFromParameterInfo(curParam);
                            }
                            overloads[i] = new SimpleOverloadResult(paramResult, Value.Name, "");
                        }

                        return overloads;*/
        }

        public override PythonMemberType MemberType {
            get {
                var type = Value.__clrtype__();
                if (type.IsEnum) {
                    return PythonMemberType.Enum;
                } else if (typeof(Delegate).IsAssignableFrom(type)) {
                    return PythonMemberType.Delegate;
                } else {
                    return PythonMemberType.Class;
                }
            }
        }

        public string Name {
            get { return PythonType.Get__name__(Value); }
        }

        public string Documentation {
            get {
                return PythonType.Get__doc__(Interpreter.CodeContext, Value) as string;
            }
        }

        public BuiltinTypeId TypeId {
            get {
                var clrType = Value.__clrtype__();

                switch (Type.GetTypeCode(Value.__clrtype__())) {
                    case TypeCode.Boolean: return BuiltinTypeId.Bool;
                    case TypeCode.Int32: return BuiltinTypeId.Int;
                    case TypeCode.String: return BuiltinTypeId.Str;
                    case TypeCode.Double: return BuiltinTypeId.Float;
                    case TypeCode.Object:
                        if (clrType == typeof(object)) {
                            return BuiltinTypeId.Object;
                        } else if (clrType == typeof(PythonFunction)) {
                            return BuiltinTypeId.Function;
                        } else if (clrType == typeof(BuiltinFunction)) {
                            return BuiltinTypeId.BuiltinFunction;
                        } else if (clrType == typeof(BuiltinMethodDescriptor)) {
                            return BuiltinTypeId.BuiltinMethodDescriptor;
                        } else if (clrType == typeof(Complex)) {
                            return BuiltinTypeId.Complex;
                        } else if (clrType == typeof(PythonDictionary)) {
                            return BuiltinTypeId.Dict;
                        } else if (clrType == typeof(BigInteger)) {
                            return BuiltinTypeId.Long;
                        } else if (clrType == typeof(List)) {
                            return BuiltinTypeId.List;
                        } else if (clrType == typeof(PythonGenerator)) {
                            return BuiltinTypeId.Generator;
                        } else if (clrType == typeof(SetCollection)) {
                            return BuiltinTypeId.Set;
                        } else if (clrType == typeof(PythonType)) {
                            return BuiltinTypeId.Type;
                        } else if (clrType == typeof(PythonTuple)) {
                            return BuiltinTypeId.Tuple;
                        }
                        break;
                }
                return BuiltinTypeId.Unknown;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                return this.Interpreter.ImportModule((string)PythonType.Get__module__(Interpreter.CodeContext, Value));
            }
        }

        public bool IsBuiltin {
            get {
                return true;
            }
        }

        public bool IsArray {
            get {
                return Value.__clrtype__().IsArray;
            }
        }

        public IPythonType GetElementType() {
            return Interpreter.GetTypeFromType(Value.__clrtype__().GetElementType());
        }

        public IList<IPythonType> GetTypesPropagatedOnCall() {
            if (typeof(Delegate).IsAssignableFrom(Value.__clrtype__())) {
                return GetEventInvokeArgs(Value.__clrtype__());
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
            get { return Value.__clrtype__().IsGenericTypeDefinition; }
        }

        public IPythonType MakeGenericType(IPythonType[] indexTypes) {
            Type[] types = new Type[indexTypes.Length];
            for (int i = 0; i < types.Length; i++) {
                types[i] = ((IronPythonType)indexTypes[i]).Value.__clrtype__();
            }
            return Interpreter.GetTypeFromType(Value.__clrtype__().MakeGenericType(types));
        }

        #endregion
    }
}

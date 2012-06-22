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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ConstantInfo : BuiltinInstanceInfo {
        private readonly object _value;
        private readonly BuiltinInstanceInfo _builtinInfo;
        private readonly PythonMemberType _memberType;
        private string _doc;

        public ConstantInfo(object value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetNamespaceFromObjects(projectState.GetTypeFromObject(value))) {
            _value = value;
            _memberType = PythonMemberType.Constant;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetNamespaceFromObjects(_type)).Instance;
        }

        public ConstantInfo(IPythonConstant value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetNamespaceFromObjects(value.Type)) {
            _value = value;
            _memberType = value.MemberType;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetNamespaceFromObjects(value.Type)).Instance;
        }

        internal static BuiltinTypeId[,] NumericResultType = MakeTypeMapping();

        /// <summary>
        /// Builds the table which defines the result of a numeric addition.
        /// 
        /// First index is the lhs, second index is the rhs, the value is the result type.
        /// </summary>
        /// <returns></returns>
        private static BuiltinTypeId[,] MakeTypeMapping() {

            const int intVal = (int)BuiltinTypeId.Int;
            const int longVal = (int)BuiltinTypeId.Long;
            const int floatVal = (int)BuiltinTypeId.Float;
            const int complexVal = (int)BuiltinTypeId.Complex;
            
            var res = new BuiltinTypeId[5,5];

            res[intVal, intVal] = BuiltinTypeId.Int;
            res[intVal, floatVal] = BuiltinTypeId.Float;
            res[intVal, complexVal] = BuiltinTypeId.Complex;
            res[intVal, longVal] = BuiltinTypeId.Long;

            res[floatVal, intVal] = BuiltinTypeId.Float;
            res[floatVal, floatVal] = BuiltinTypeId.Float;
            res[floatVal, complexVal] = BuiltinTypeId.Complex;
            res[floatVal, longVal] = BuiltinTypeId.Float;

            res[longVal, intVal] = BuiltinTypeId.Long;
            res[longVal, floatVal] = BuiltinTypeId.Float;
            res[longVal, complexVal] = BuiltinTypeId.Complex;
            res[longVal, longVal] = BuiltinTypeId.Long;

            res[complexVal, intVal] = BuiltinTypeId.Complex;
            res[complexVal, floatVal] = BuiltinTypeId.Complex;
            res[complexVal, complexVal] = BuiltinTypeId.Complex;
            res[complexVal, longVal] = BuiltinTypeId.Complex;
            
            return res;
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            return NumericOp(node, this, unit, operation, rhs) ?? _builtinInfo.BinaryOperation(node, unit, operation, rhs);
        }

        public override ISet<Namespace> ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            return SelfSet;
        }

        internal static ISet<Namespace> NumericOp(Node node, BuiltinInstanceInfo lhs, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            BuiltinTypeId curType = lhs.TypeId;
            switch (operation) {
                case PythonOperator.TrueDivide:
                    if (curType == BuiltinTypeId.Int || curType == BuiltinTypeId.Long) {
                        bool intsOnly = true, rhsInt = false;
                        foreach (var type in rhs) {
                            var rhsType = type.TypeId;
                            if (rhsType == BuiltinTypeId.Int|| rhsType == BuiltinTypeId.Long) {
                                rhsInt = true;
                            } else {
                                intsOnly = false;
                            }
                        }

                        if (rhsInt) {
                            if (intsOnly) {
                                return unit.ProjectState._floatType;
                            }
                            goto case PythonOperator.Add;
                        }
                    }
                    break;
                case PythonOperator.Mod:
                    if (lhs.TypeId == BuiltinTypeId.Str || lhs.TypeId == BuiltinTypeId.Bytes) {
                        return lhs.ClassInfo.Instance;
                    }
                    goto case PythonOperator.Add;
                case PythonOperator.Multiply:
                    if (curType == BuiltinTypeId.Str || curType == BuiltinTypeId.Bytes) {
                        foreach (var type in rhs) {
                            var rhsType = type.TypeId;
                            if (rhsType == BuiltinTypeId.Int || rhsType == BuiltinTypeId.Long) {
                                return lhs.ClassInfo.Instance;
                            }
                        }
                    } else if (curType == BuiltinTypeId.Int || curType == BuiltinTypeId.Long) {
                        foreach (var type in rhs) {
                            var rhsType = type.TypeId;
                            if (rhsType == BuiltinTypeId.Str || rhsType == BuiltinTypeId.Bytes) {
                                return type.SelfSet;
                            }
                        }
                    }

                    goto case PythonOperator.Add;
                case PythonOperator.Add:
                case PythonOperator.Subtract:
                case PythonOperator.Divide:
                case PythonOperator.BitwiseAnd:
                case PythonOperator.BitwiseOr:
                case PythonOperator.Xor:
                case PythonOperator.LeftShift:
                case PythonOperator.RightShift:
                case PythonOperator.Power:
                case PythonOperator.FloorDivide:
                    ISet<Namespace> res = EmptySet<Namespace>.Instance;
                    bool madeSet = false;

                    foreach (var type in rhs) {
                        var typeId = type.TypeId;

                        if (curType <= BuiltinTypeId.Complex && typeId <= BuiltinTypeId.Complex) {
                            switch (NumericResultType[(int)curType, (int)typeId]) {
                                case BuiltinTypeId.Complex: res = res.Union(unit.ProjectState._complexType, ref madeSet); break;
                                case BuiltinTypeId.Long: res = res.Union(unit.ProjectState._floatType, ref madeSet); break;
                                case BuiltinTypeId.Float: res = res.Union(unit.ProjectState._floatType, ref madeSet); break;
                                case BuiltinTypeId.Int: res = res.Union(unit.ProjectState._intType, ref madeSet); break;
                                default:
                                    res = res.Union(type.ReverseBinaryOperation(node, unit, operation, lhs), ref madeSet);
                                    break;
                            }
                        } else {
                            res = res.Union(type.ReverseBinaryOperation(node, unit, operation, lhs), ref madeSet);
                        }
                    }
                    return res;
            }
            return null;
        }

        public override ISet<Namespace> UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return _builtinInfo.UnaryOperation(node, unit, operation);
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            return _builtinInfo.Call(node, unit, args, keywordArgNames);
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            _builtinInfo.AugmentAssign(node, unit, value);
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            return _builtinInfo.GetDescriptor(node, instance, context, unit);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return _builtinInfo.GetMember(node, unit, name);
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            _builtinInfo.SetMember(node, unit, name, value);
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            // indexing/slicing strings should return the string type.
            if (_value is AsciiString) {                
                return ProjectState._bytesType.Instance.SelfSet;
            } else if (_value is string) {
                return ProjectState._unicodeType.Instance.SelfSet;
            }

            return base.GetIndex(node, unit, index);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            _builtinInfo.SetIndex(node, unit, index, value);
        }

        public override ISet<Namespace> GetStaticDescriptor(AnalysisUnit unit) {
            return _builtinInfo.GetStaticDescriptor(unit);
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return _builtinInfo.GetAllMembers(moduleContext);
        }

        public override string Description {
            get {
                if (_value == null) {
                    return "None";
                }

                return _type.Name;
                //return PythonOps.Repr(ProjectState.CodeContext, _value);
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    object docObj = _type.Documentation;
                    _doc = docObj == null ? "" : Utils.StripDocumentation(docObj.ToString());
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return _memberType;
            }
        }

        public override string ToString() {
            return "<ConstantInfo object '" + Description + "'" + (_value == null ? "" : (" '" + _value.ToString() + "' ")) + ">"; // " at " + hex(id(self))
        }

        public override object GetConstantValue() {
            return _value;
        }

        public override bool UnionEquals(Namespace ns) {
            ConstantInfo ci = ns as ConstantInfo;
            if (ci == null) {
                BuiltinInstanceInfo bi = ns as BuiltinInstanceInfo;
                if (bi != null) {
                    return bi.ClassInfo == ClassInfo;
                }
                return false;
            } else if (ci._value == null) {
                return _value == null;
            } else if (ci._value.Equals(_value)) {
                return true;
            }

            return ci.ClassInfo == ClassInfo;
        }

        public override int UnionHashCode() {
            if (_value == null) {
                return 0;
            }

            return ClassInfo.GetHashCode();
        }

        public object Value {
            get {
                return _value;
            }
        }

        public override BuiltinTypeId TypeId {
            get {
                return ClassInfo.PythonType.TypeId;
            }
        }
    }
}

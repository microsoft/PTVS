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

        internal ConstantInfo(BuiltinClassInfo klass, object value)
            : base(klass) {
            _value = value;
            _memberType = PythonMemberType.Constant;
            _builtinInfo = klass.Instance;
        }

        public ConstantInfo(object value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetAnalysisValueFromObjectsThrowOnNull(projectState.GetTypeFromObject(value))) {
            _value = value;
            _memberType = PythonMemberType.Constant;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetAnalysisValueFromObjects(_type)).Instance;
        }

        public ConstantInfo(IPythonConstant value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetAnalysisValueFromObjects(value.Type)) {
            _value = value;
            _memberType = value.MemberType;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetAnalysisValueFromObjects(value.Type)).Instance;
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return NumericOp(node, this, unit, operation, rhs) ?? _builtinInfo.BinaryOperation(node, unit, operation, rhs);
        }

        internal static IAnalysisSet NumericOp(Node node, BuiltinInstanceInfo lhs, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            var res = AnalysisSet.Empty;
            var lhsType = lhs.TypeId;

            foreach(var ns in rhs) {
                var rhsType = ns.TypeId;

                // First handle string operations
                if (lhsType == BuiltinTypeId.Bytes || lhsType == BuiltinTypeId.Unicode) {
                    if (operation == PythonOperator.Mod) {
                        res = res.Union(lhs.ClassInfo.Instance);
                    } else if (operation == PythonOperator.Add &&
                        (rhsType == BuiltinTypeId.Bytes || rhsType == BuiltinTypeId.Unicode)) {
                        res = res.Union(lhs.ClassInfo.Instance);
                    } else if (operation == PythonOperator.Multiply &&
                        (rhsType == BuiltinTypeId.Int || rhsType == BuiltinTypeId.Long)) {
                        res = res.Union(lhs.ClassInfo.Instance);
                    }
                    continue;
                } else if (operation == PythonOperator.Multiply &&
                           (lhsType == BuiltinTypeId.Int || lhsType == BuiltinTypeId.Long)) {
                    if (rhsType == BuiltinTypeId.Str || rhsType == BuiltinTypeId.Bytes || rhsType == BuiltinTypeId.Unicode ||
                        rhsType == BuiltinTypeId.Tuple || rhsType == BuiltinTypeId.List) {
                        res = res.Union(unit.ProjectState.ClassInfos[rhsType].Instance);
                        continue;
                    }
                }

                // These specializations change rhsType before type promotion
                // rules are applied.
                if ((operation == PythonOperator.TrueDivide || 
                    (operation == PythonOperator.Divide && unit.ProjectState.LanguageVersion.Is3x())) &&
                    (lhsType == BuiltinTypeId.Int || lhsType == BuiltinTypeId.Long) &&
                    (rhsType == BuiltinTypeId.Int || rhsType == BuiltinTypeId.Long)) {
                    rhsType = BuiltinTypeId.Float;
                }

                // Type promotion rules are applied 
                if (lhsType == BuiltinTypeId.Unknown || lhsType > BuiltinTypeId.Complex || 
                    rhsType == BuiltinTypeId.Unknown || rhsType > BuiltinTypeId.Complex) {
                    // Non-numeric types require the reverse operation
                    res = res.Union(ns.ReverseBinaryOperation(node, unit, operation, lhs));
                } else if (lhsType == BuiltinTypeId.Complex || rhsType == BuiltinTypeId.Complex) {
                    res = res.Union(unit.ProjectState.ClassInfos[BuiltinTypeId.Complex].Instance);
                } else if (lhsType == BuiltinTypeId.Float || rhsType == BuiltinTypeId.Float) {
                    res = res.Union(unit.ProjectState.ClassInfos[BuiltinTypeId.Float].Instance);
                } else if (lhsType == BuiltinTypeId.Long || rhsType == BuiltinTypeId.Long) {
                    res = res.Union(unit.ProjectState.ClassInfos[BuiltinTypeId.Long].Instance);
                } else {
                    res = res.Union(unit.ProjectState.ClassInfos[BuiltinTypeId.Int].Instance);
                }
            }

            return res.Count > 0 ? res : null;
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return _builtinInfo.UnaryOperation(node, unit, operation);
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _builtinInfo.Call(node, unit, args, keywordArgNames);
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            _builtinInfo.AugmentAssign(node, unit, value);
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            return _builtinInfo.GetDescriptor(node, instance, context, unit);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            return _builtinInfo.GetMember(node, unit, name);
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            _builtinInfo.SetMember(node, unit, name, value);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            // indexing/slicing strings should return the string type.
            if (_value is AsciiString || _value is string) {
                return ClassInfo.Instance;
            }

            return base.GetIndex(node, unit, index);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            _builtinInfo.SetIndex(node, unit, index, value);
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
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
            var valueStr = (_value == null || _value is IPythonConstant) ? "" : (" '" + _value.ToString() + "'");
            valueStr = valueStr.Replace("\r", "\\r").Replace("\n", "\\n");
            return "<" + Description + valueStr + ">"; // " at " + hex(id(self))
        }

        public override object GetConstantValue() {
            return _value;
        }

        // Union merging for ConstantInfo is handled in BuiltinInstanceInfo.

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var ci = (ConstantInfo)obj;
            if (ci._value == null) {
                return _value == null;
            } else {
                return ci._value.Equals(_value);
            }
        }

        public override int GetHashCode() {
            if (_value == null) {
                return 0;
            }

            return _value.GetHashCode();
        }

        public object Value {
            get {
                return _value;
            }
        }

        internal override BuiltinTypeId TypeId {
            get {
                return ClassInfo.PythonType.TypeId;
            }
        }
    }
}

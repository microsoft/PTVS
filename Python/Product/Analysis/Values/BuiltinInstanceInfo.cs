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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class BuiltinInstanceInfo : BuiltinNamespace<IPythonType>, IReferenceableContainer {
        private readonly BuiltinClassInfo _klass;

        public BuiltinInstanceInfo(BuiltinClassInfo klass)
            : base(klass._type, klass.ProjectState) {
            _klass = klass;
        }

        public BuiltinClassInfo ClassInfo {
            get {
                return _klass;
            }
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        public override IAnalysisSet GetInstanceType() {
            if (_klass.TypeId == BuiltinTypeId.Type) {
                return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;
            }
            return base.GetInstanceType();
        }

        public override string Description {
            get {
                return _klass._type.Name;
            }
        }

        public override string Documentation {
            get {
                return _klass.Documentation;
            }
        }

        public override PythonMemberType MemberType {
            get {
                switch (_klass.MemberType) {
                    case PythonMemberType.Enum: return PythonMemberType.EnumInstance;
                    case PythonMemberType.Delegate: return PythonMemberType.DelegateInstance;
                    default:
                        return PythonMemberType.Instance;
                }
            }
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
                return res.GetDescriptor(node, this, _klass, unit);
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _klass.AddMemberReference(node, unit, name);
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            if (operation == PythonOperator.Not) {
                return unit.ProjectState.ClassInfos[BuiltinTypeId.Bool].Instance;
            }

            string methodName = InstanceInfo.UnaryOpToString(unit.ProjectState, operation);
            if (methodName != null) {
                var method = GetMember(node, unit, methodName);
                if (method.Count > 0) {
                    var res = method.Call(
                        node,
                        unit,
                        new[] { this },
                        ExpressionEvaluator.EmptyNames
                    );

                    return res;
                }
            }
            return base.UnaryOperation(node, unit, operation);
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return ConstantInfo.NumericOp(node, this, unit, operation, rhs) ?? NumericOp(node, unit, operation, rhs) ?? AnalysisSet.Empty;
        }

        private IAnalysisSet NumericOp(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, IAnalysisSet rhs) {
            string methodName = InstanceInfo.BinaryOpToString(operation);
            if (methodName != null) {
                var method = GetMember(node, unit, methodName);
                if (method.Count > 0) {
                    var res = method.Call(
                        node,
                        unit,
                        new[] { this, rhs },
                        ExpressionEvaluator.EmptyNames
                    );

                    if (res.IsObjectOrUnknown()) {
                        // the type defines the operator, assume it returns 
                        // some combination of the input types.
                        return SelfSet.Union(rhs);
                    }

                    return res;
                }
            }

            return base.BinaryOperation(node, unit, operation, rhs);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var getItem = GetMember(node, unit, "__getitem__");
            if (getItem.Count > 0) {
                var res = getItem.Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);
                if (res.IsObjectOrUnknown() && index.Contains(SliceInfo.Instance)) {
                    // assume slicing returns a type of the same object...
                    return this;
                }
                return res;
            }
            return AnalysisSet.Empty;
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                IAnalysisSet callRes;
                if (_klass.GetAllMembers(ProjectState._defaultContext).TryGetValue("__call__", out callRes)) {
                    foreach (var overload in callRes.SelectMany(av => av.Overloads)) {
                        yield return overload.WithNewParameters(
                            overload.Parameters.Skip(1).ToArray()
                        );
                    }
                }

                foreach (var overload in base.Overloads) {
                    yield return overload;
                }
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = base.Call(node, unit, args, keywordArgNames);

            if (Push()) {
                try {
                    var callRes = GetMember(node, unit, "__call__");
                    if (callRes.Any()) {
                        res = res.Union(callRes.Call(node, unit, args, keywordArgNames));
                    }
                } finally {
                    Pop();
                }
            }
            
            return res;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (Push()) {
                try {
                    var iter = GetIterator(node, unit);
                    if (iter.Any()) {
                        return iter
                            .GetMember(node, unit, unit.ProjectState.LanguageVersion.Is3x() ? "__next__" : "next")
                            .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);
                    }
                } finally {
                    Pop();
                }
            }

            return base.GetEnumeratorTypes(node, unit);
        }

        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (unit.ProjectState.LanguageVersion.Is3x() && Push()) {
                try {
                    var iter = GetAsyncIterator(node, unit);
                    if (iter.Any()) {
                        return iter
                            .GetMember(node, unit, "__anext__")
                            .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames)
                            .Await(node, unit);
                    }
                } finally {
                    Pop();
                }
            }

            return base.GetAsyncEnumeratorTypes(node, unit);
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            if (klass.Contains(this.ClassInfo)) {
                return true;
            }

            if (TypeId != BuiltinTypeId.NoneType &&
                TypeId != BuiltinTypeId.Type &&
                TypeId != BuiltinTypeId.Function &&
                TypeId != BuiltinTypeId.BuiltinFunction) {
                return klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Object]);
            }

            return false;
        }

        internal override BuiltinTypeId TypeId {
            get {
                return ClassInfo.PythonType.TypeId;
            }
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var dict = ProjectState.ClassInfos[BuiltinTypeId.Dict];
            if (strength < MergeStrength.IgnoreIterableNode && (this is DictionaryInfo || this == dict.Instance)) {
                if (ns is DictionaryInfo || ns == dict.Instance) {
                    return true;
                }
                var ci = ns as ConstantInfo;
                if (ci != null && ci.ClassInfo == dict) {
                    return true;
                }
                return false;
            }
            
            if (strength >= MergeStrength.ToObject) {
                if (TypeId == BuiltinTypeId.NoneType || ns.TypeId == BuiltinTypeId.NoneType) {
                    // BII + BII(None) => do not merge
                    // Unless both types are None, since they could be various
                    // combinations of BuiltinInstanceInfo or ConstantInfo that
                    // need to be merged.
                    return TypeId == BuiltinTypeId.NoneType && ns.TypeId == BuiltinTypeId.NoneType;
                }

                var func = ProjectState.ClassInfos[BuiltinTypeId.Function];
                if (this == func.Instance) {
                    // FI + BII(function) => BII(function)
                    return ns is FunctionInfo || ns is BuiltinFunctionInfo || ns == func.Instance;
                } else if (ns == func.Instance) {
                    return false;
                }

                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                if (this == type.Instance) {
                    // CI + BII(type) => BII(type)
                    // BCI + BII(type) => BII(type)
                    return ns is ClassInfo || ns is BuiltinClassInfo || ns == type.Instance;
                } else if (ns == type.Instance) {
                    return false;
                }

                /// BII + II => BII(object)
                /// BII + BII => BII(object)
                return ns is InstanceInfo || ns is BuiltinInstanceInfo;

            } else if (strength >= MergeStrength.ToBaseClass) {
                var bii = ns as BuiltinInstanceInfo;
                if (bii != null) {
                    return ClassInfo.UnionEquals(bii.ClassInfo, strength);
                }
                var ii = ns as InstanceInfo;
                if (ii != null) {
                    return ClassInfo.UnionEquals(ii.ClassInfo, strength);
                }
            } else if (this is ConstantInfo || ns is ConstantInfo) {
                // ConI + BII => BII if CIs match
                var bii = ns as BuiltinInstanceInfo;
                return bii != null && ClassInfo.Equals(bii.ClassInfo);
            }

            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            return ClassInfo.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                if (TypeId == BuiltinTypeId.NoneType || ns.TypeId == BuiltinTypeId.NoneType) {
                    // BII + BII(None) => do not merge
                    // Unless both types are None, since they could be various
                    // combinations of BuiltinInstanceInfo or ConstantInfo that
                    // need to be merged.
                    return ProjectState.ClassInfos[BuiltinTypeId.NoneType].Instance;
                }

                var func = ProjectState.ClassInfos[BuiltinTypeId.Function];
                if (this == func.Instance) {
                    // FI + BII(function) => BII(function)
                    return func.Instance;
                }

                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                if (this == type.Instance) {
                    // CI + BII(type) => BII(type)
                    // BCI + BII(type) => BII(type)
                    return type;
                }

                /// BII + II => BII(object)
                /// BII + BII => BII(object)
                return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;

            } else if (strength >= MergeStrength.ToBaseClass) {
                var bii = ns as BuiltinInstanceInfo;
                if (bii != null) {
                    return ClassInfo.UnionMergeTypes(bii.ClassInfo, strength).GetInstanceType().Single();
                }
                var ii = ns as InstanceInfo;
                if (ii != null) {
                    return ClassInfo.UnionMergeTypes(ii.ClassInfo, strength).GetInstanceType().Single();
                }
            } else if (this is ConstantInfo || ns is ConstantInfo) {
                return ClassInfo.Instance;
            }

            return base.UnionMergeTypes(ns, strength);
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _klass.GetDefinitions(name);
        }

        #endregion
    }
}

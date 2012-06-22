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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents an instance of a class implemented in Python
    /// </summary>
    internal class InstanceInfo : Namespace, IReferenceableContainer {
        private readonly ClassInfo _classInfo;
        private Dictionary<string, VariableDef> _instanceAttrs;

        public InstanceInfo(ClassInfo classInfo) {
            _classInfo = classInfo;
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            var res = new Dictionary<string, ISet<Namespace>>();
            if (_instanceAttrs != null) {
                foreach (var kvp in _instanceAttrs) {
                    var types = kvp.Value.Types;
                    var key = kvp.Key;
                    kvp.Value.ClearOldValues();
                    if (kvp.Value.VariableStillExists) {
                        MergeTypes(res, key, types);
                    }
                }
            }

            // check and see if it's defined in a base class instance as well...
            foreach (var b in _classInfo.Bases) {
                foreach (var ns in b) {
                    if (ns.Push()) {
                        try {
                            ClassInfo baseClass = ns as ClassInfo;
                            if (baseClass != null &&
                                baseClass.Instance._instanceAttrs != null) {
                                foreach (var kvp in baseClass.Instance._instanceAttrs) {
                                    kvp.Value.ClearOldValues();
                                    if (kvp.Value.VariableStillExists) {
                                        MergeTypes(res, kvp.Key, kvp.Value.Types);
                                    }
                                }
                            }
                        } finally {
                            ns.Pop();
                        }
                    }
                }
            }

            foreach (var classMem in _classInfo.GetAllMembers(moduleContext)) {
                MergeTypes(res, classMem.Key, classMem.Value);
            }
            return res;
        }

        private static void MergeTypes(Dictionary<string, ISet<Namespace>> res, string key, IEnumerable<Namespace> types) {
            ISet<Namespace> set;
            if (!res.TryGetValue(key, out set)) {
                res[key] = set = new HashSet<Namespace>();
            }

            set.UnionWith(types);
        }

        public Dictionary<string, VariableDef> InstanceAttributes {
            get {
                return _instanceAttrs;
            }
        }

        public PythonAnalyzer ProjectState {
            get {
                return _classInfo._analysisUnit.ProjectState;
            }
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            // __getattribute__ takes precedence over everything.
            ISet<Namespace> getattrRes = EmptySet<Namespace>.Instance;
            var getAttribute = _classInfo.GetMemberNoReferences(node, unit.CopyForEval(), "__getattribute__");
            if (getAttribute.Count > 0) {
                foreach (var getAttrFunc in getAttribute) {
                    var func = getAttrFunc as BuiltinMethodInfo;
                    if (func != null && func.Function.Overloads.Count == 1 && func.Function.DeclaringType == ProjectState.Types.Object) {
                        continue;
                    }
                    // TODO: We should really do a get descriptor / call here
                    // FIXME: new string[0]
                    getattrRes = getattrRes.Union(getAttrFunc.Call(node, unit, new[] { SelfSet, ProjectState._stringType.Instance.SelfSet }, ExpressionEvaluator.EmptyNames));
                }
                if (getattrRes.Count > 0) {
                    return getattrRes;
                }
            }
            
            // then check class members
            var classMem = _classInfo.GetMemberNoReferences(node, unit, name);
            if (classMem.Count > 0) {
                var desc = classMem.GetDescriptor(node, this, _classInfo, unit);
                if (desc.Count > 0) {
                    // TODO: Check if it's a data descriptor...
                    return desc;
                }
            } else {
                // if the class gets a value later we need to be re-analyzed
                _classInfo.Scope.CreateEphemeralVariable(node, unit, name, false).AddDependency(unit);
            }
           
            // ok, it most be an instance member...
            if (_instanceAttrs == null) {
                _instanceAttrs = new Dictionary<string, VariableDef>();
            }
            VariableDef def;
            if (!_instanceAttrs.TryGetValue(name, out def)) {
                _instanceAttrs[name] = def = new EphemeralVariableDef();
            }
            def.AddReference(node, unit);
            def.AddDependency(unit);

            // check and see if it's defined in a base class instance as well...
            var res = def.Types;
            bool madeSet = false;
            foreach (var b in _classInfo.Bases) {
                foreach (var ns in b) {
                    if (ns.Push()) {
                        try {
                            ClassInfo baseClass = ns as ClassInfo;
                            if (baseClass != null &&
                                baseClass.Instance._instanceAttrs != null &&
                                baseClass.Instance._instanceAttrs.TryGetValue(name, out def)) {
                                res = res.Union(def.Types, ref madeSet);
                            }
                        } finally {
                            ns.Pop();
                        }
                    }
                }
            }
            
            if (res.Count == 0) {
                // and if that doesn't exist fall back to __getattr__
                var getAttr = _classInfo.GetMemberNoReferences(node, unit, "__getattr__");
                if (getAttr.Count > 0) {
                    foreach (var getAttrFunc in getAttr) {
                        // TODO: We should really do a get descriptor / call here
                        //FIXME: new string[0]
                        getattrRes = getattrRes.Union(getAttrFunc.Call(node, unit, new[] { SelfSet, _classInfo._analysisUnit.ProjectState._stringType.Instance.SelfSet }, ExpressionEvaluator.EmptyNames));
                    }
                }
                return getattrRes;
            }
            return res;
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            var getter = _classInfo.GetMemberNoReferences(node, unit, "__get__");
            if (getter.Count > 0) {
                var get = getter.GetDescriptor(node, this, _classInfo, unit);
                return get.Call(node, unit, new[] { instance, context }, ExpressionEvaluator.EmptyNames);
            }
            return SelfSet;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            if (_instanceAttrs == null) {
                _instanceAttrs = new Dictionary<string, VariableDef>();
            }

            VariableDef instMember;
            if (!_instanceAttrs.TryGetValue(name, out instMember) || instMember == null) {
                _instanceAttrs[name] = instMember = new VariableDef();
            }
            instMember.AddAssignment(node, unit);
            instMember.AddTypes(unit, value);
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            if (_instanceAttrs == null) {
                _instanceAttrs = new Dictionary<string, VariableDef>();
            }
            
            VariableDef instMember;
            if (!_instanceAttrs.TryGetValue(name, out instMember) || instMember == null) {
                _instanceAttrs[name] = instMember = new VariableDef();
            }

            instMember.AddReference(node, unit);

            _classInfo.GetMember(node, unit, name);
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, ISet<Namespace> rhs) {
            string op = null;
            switch (operation) {
                case PythonOperator.Multiply: op = "__mul__"; break;
                case PythonOperator.Add: op = "__add__"; break;
                case PythonOperator.Subtract: op = "__sub__"; break;
                case PythonOperator.Xor: op = "__xor__"; break;
                case PythonOperator.BitwiseAnd: op = "__and__"; break;
                case PythonOperator.BitwiseOr: op = "__or__"; break;
                case PythonOperator.Divide: op = "__div__"; break;
                case PythonOperator.FloorDivide: op = "__floordiv__"; break;
                case PythonOperator.LeftShift: op = "__lshift__"; break;
                case PythonOperator.Mod: op = "__mod__"; break;
                case PythonOperator.Power: op = "__pow__"; break;
                case PythonOperator.RightShift: op = "__rshift__"; break;
                case PythonOperator.TrueDivide: op = "__truediv__"; break;
            }

            if (op != null) {
                var invokeMem = GetMember(node, unit, op);
                if (invokeMem.Count > 0) {
                    // call __*__ method
                    return invokeMem.Call(node, unit, new[] { rhs }, ExpressionEvaluator.EmptyNames);
                }
            }

            return base.BinaryOperation(node, unit, operation, rhs);
        }

        public override ISet<Namespace> ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            string op = null;
            switch (operation) {
                case PythonOperator.Multiply: op = "__rmul__"; break;
                case PythonOperator.Add: op = "__radd__"; break;
                case PythonOperator.Subtract: op = "__rsub__"; break;
                case PythonOperator.Xor: op = "__rxor__"; break;
                case PythonOperator.BitwiseAnd: op = "__rand__"; break;
                case PythonOperator.BitwiseOr: op = "__ror__"; break;
                case PythonOperator.Divide: op = "__rdiv__"; break;
                case PythonOperator.FloorDivide: op = "__rfloordiv__"; break;
                case PythonOperator.LeftShift: op = "__rlshift__"; break;
                case PythonOperator.Mod: op = "__rmod__"; break;
                case PythonOperator.Power: op = "__rpow__"; break;
                case PythonOperator.RightShift: op = "__rrshift__"; break;
                case PythonOperator.TrueDivide: op = "__rtruediv__"; break;
            }

            if (op != null) {
                var invokeMem = GetMember(node, unit, op);
                if (invokeMem.Count > 0) {
                    // call __r*__ method
                    return invokeMem.Call(node, unit, new[] { rhs }, ExpressionEvaluator.EmptyNames);
                }
            }

            return base.ReverseBinaryOperation(node, unit, operation, rhs);
        }

        public override ProjectEntry DeclaringModule {
            get {
                return _classInfo.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _classInfo.DeclaringVersion;
            }
        }

        public override string Description {
            get {
                return ClassInfo.ClassDefinition.Name + " instance";
            }
        }

        public override string Documentation {
            get {
                return ClassInfo.Documentation;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Instance;
            }
        }

        public ClassInfo ClassInfo {
            get { return _classInfo; }
        }

        public override string ToString() {
            return ClassInfo._analysisUnit.FullName + " instance";
        }

        public override bool UnionEquals(Namespace ns) {
            if (Object.ReferenceEquals(this, ns)) {
                return true;
            }

            InstanceInfo inst = ns as InstanceInfo;
            if (inst == null) {
                return false;
            }

            return inst.ClassInfo.UnionEquals(ClassInfo);
        }

        public override int UnionHashCode() {
            return ClassInfo.UnionHashCode();
        }

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            VariableDef def;
            if (_instanceAttrs != null && _instanceAttrs.TryGetValue(name, out def)) {
                yield return def;
            }

            foreach (var classDef in _classInfo.GetDefinitions(name)) {
                yield return classDef;
            }
        }

        #endregion
    }
}

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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a class implemented in Python
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

                    MergeTypes(res, key, types);
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
                    getattrRes = getattrRes.Union(getAttrFunc.Call(node, unit, new[] { SelfSet, ProjectState._stringType.Instance.SelfSet }, new string[0]));
                }
                if (getattrRes.Count > 0) {
                    return getattrRes;
                }
            }
            
            // then check class members
            var classMem = _classInfo.GetMemberNoReferences(node, unit, name).GetDescriptor(this, unit);
            if (classMem.Count > 0) {
                // TODO: Check if it's a data descriptor...
                return classMem;
            }

            // ok, it most be an instance member...
            if (_instanceAttrs == null) {
                _instanceAttrs = new Dictionary<string, VariableDef>();
            }
            VariableDef def;
            if (!_instanceAttrs.TryGetValue(name, out def)) {
                _instanceAttrs[name] = def = new VariableDef();
            }
            def.AddReference(node, unit);
            def.AddDependency(unit);

            var res = def.Types;
            if (res.Count == 0) {
                // and if that doesn't exist fall back to __getattr__
                var getAttr = _classInfo.GetMemberNoReferences(node, unit, "__getattr__");
                if (getAttr.Count > 0) {
                    foreach (var getAttrFunc in getAttr) {
                        // TODO: We should really do a get descriptor / call here
                        //FIXME: new string[0]
                        getattrRes = getattrRes.Union(getAttrFunc.Call(node, unit, new[] { SelfSet, _classInfo._analysisUnit.ProjectState._stringType.Instance.SelfSet }, new string[0]));
                    }
                }
                return getattrRes;
            }
            return res;
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
            instMember.AddTypes(node, unit, value);
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

        public override PythonMemberType ResultType {
            get {
                return PythonMemberType.Instance;
            }
        }

        public ClassInfo ClassInfo {
            get { return _classInfo; }
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

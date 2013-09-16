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
using System.Collections.ObjectModel;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinFunctionInfo : BuiltinNamespace<IPythonType> {
        private IPythonFunction _function;
        private string _doc;
        private ReadOnlyCollection<OverloadResult> _overloads;
        private readonly IAnalysisSet _returnTypes;
        private BuiltinMethodInfo _method;

        public BuiltinFunctionInfo(IPythonFunction function, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.BuiltinFunction], projectState) {

            _function = function;
            _returnTypes = Utils.GetReturnTypes(function, projectState);
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            return klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Function]) ||
                klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.BuiltinFunction]);
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _returnTypes.GetInstanceType();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (_function.IsStatic || instance.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.NoneType])) {
                return base.GetDescriptor(node, instance, context, unit);
            } else if (_method == null) {
                _method = new BuiltinMethodInfo(_function, PythonMemberType.Method, ProjectState);
            }

            return _method.GetDescriptor(node, instance, context, unit);
        }

        public IPythonFunction Function {
            get {
                return _function;
            }
        }

        public override string Name {
            get {
                return _function.Name;
            }
        }

        public override string Description {
            get {
                string res;
                if (_function.IsBuiltin) {
                    res = "built-in function " + _function.Name;
                } else {
                    res = "function " + _function.Name;
                }

                if (!string.IsNullOrWhiteSpace(Documentation)) {
                    res += System.Environment.NewLine + Documentation;
                }
                return res;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = _function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(ProjectState, _function.Name, overloads[i], 0, GetDoc);
                    }
                    _overloads = new ReadOnlyCollection<OverloadResult>(result);
                }
                return _overloads;
            }
        }

        // can't create delegate to property...
        private string GetDoc() {
            return Documentation;
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_function.Documentation);
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return _function.MemberType;
            }
        }

        public override ILocatedMember GetLocatedMember() {
            return _function as ILocatedMember;
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ns is BuiltinFunctionInfo || ns is FunctionInfo || ns == ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.UnionHashCode(strength);
            }
            return base.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionMergeTypes(ns, strength);
        }
    }
}

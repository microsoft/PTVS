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
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BoundMethodInfo : Namespace {
        private readonly FunctionInfo _function;
        private readonly Namespace _instanceInfo;

        public BoundMethodInfo(FunctionInfo function, Namespace instance) {
            _function = function;
            _instanceInfo = instance;
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return _function.AnalysisUnit;
            }
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            return _function.Call(node, unit, Utils.Concat(_instanceInfo.SelfSet, args), keywordArgNames);
        }

        public FunctionInfo Function {
            get {
                return _function;
            }
        }

        public override ProjectEntry DeclaringModule {
            get {
                return _function.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _function.DeclaringVersion;
            }
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                return _function.Locations;
            }
        }

        public override string Description {
            get {
                var result = new StringBuilder();
                result.Append("method ");
                result.Append(_function.FunctionDefinition.Name);
                
                if (_instanceInfo is InstanceInfo) {
                    result.Append(" of ");
                    result.Append(((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name);
                    result.Append(" objects ");
                }

                _function.AddReturnTypeString(result);
                _function.AddDocumentationString(result);

                return result.ToString();
            }
        }

        public override string ShortDescription {
            get {
                var result = new StringBuilder();
                result.Append("method ");
                result.Append(_function.FunctionDefinition.Name);

                if (_instanceInfo is InstanceInfo) {
                    result.Append(" of ");
                    result.Append(((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name);
                    result.Append(" objects ");
                }

                return result.ToString();
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                var p = _function.FunctionDefinition.Parameters;

                var pp = p.Count == 0 ? new ParameterResult[0] : new ParameterResult[p.Count - 1];
                for (int i = 1; i < p.Count; i++) {
                    pp[i - 1] = FunctionInfo.MakeParameterResult(_function.ProjectState, p[i], DeclaringModule.Tree);
                }
                string doc = _function.Documentation;

                return new ReadOnlyCollection<OverloadResult>(
                    new OverloadResult[] {
                        new SimpleOverloadResult(pp, _function.FunctionDefinition.Name, doc)
                    }
                );
            }
        }

        public override string Documentation {
            get {
                return _function.Documentation;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Method;
            }
        }

        public override string ToString() {
            var name = _function.FunctionDefinition.Name;
            return "Method" /* + hex(id(self)) */ + " " + name;
        }

        internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            if (bmi == null || (Function.Equals(bmi.Function) && _instanceInfo.Equals(bmi._instanceInfo))) {
                return this;
            } else {
                var newFunc = Function.UnionMergeTypes(bmi.Function, strength) as FunctionInfo;
                var newInst = _instanceInfo.UnionMergeTypes(bmi._instanceInfo, strength);
                if (newFunc != null && newInst != null &&
                    (!Object.ReferenceEquals(newFunc, Function) || !Object.ReferenceEquals(newInst, _instanceInfo))) {
                    return new BoundMethodInfo(newFunc, newInst);
                }
            }
            return this;
        }

        public override bool UnionEquals(Namespace ns, int strength) {
            var bmi = ns as BoundMethodInfo;
            return bmi != null && _instanceInfo.UnionEquals(bmi._instanceInfo, strength) && Function.UnionEquals(bmi.Function, strength);
        }

        public override int UnionHashCode(int strength) {
            return _instanceInfo.UnionHashCode(strength) ^ Function.UnionHashCode(strength);
        }
    }
}

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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BoundMethodInfo : UserDefinedInfo {
        private readonly FunctionInfo _function;
        private readonly Namespace _instanceInfo;

        public BoundMethodInfo(FunctionInfo function, Namespace instance)
            : base(function._analysisUnit) {
            _function = function;
            _instanceInfo = instance;
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
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
                var res = "method " + _function.FunctionDefinition.Name;
                if (_instanceInfo is InstanceInfo) {
                    res += " of " + ((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name + " objects ";
                }
                if (!String.IsNullOrEmpty(_function.FunctionDescription)) {
                    res += _function.FunctionDescription + Environment.NewLine;
                }

                if (!String.IsNullOrEmpty(_function.Documentation)) {
                    res += Environment.NewLine + _function.Documentation;
                }
                return res;
            }
        }

        public override string ShortDescription {
            get {
                return Description;/*
                var res = "method " + _function.FunctionDefinition.Name;
                if (_instanceInfo is InstanceInfo) {
                    res += " of " + ((InstanceInfo)_instanceInfo).ClassInfo.ClassDefinition.Name + " objects" + Environment.NewLine;
                }
                return res;*/
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                var p = _function.FunctionDefinition.Parameters;

                var pp = p.Count == 0 ? new ParameterResult[0] : new ParameterResult[p.Count - 1];
                for (int i = 1; i < p.Count; i++) {
                    pp[i - 1] = FunctionInfo.MakeParameterResult(_function.ProjectState, p[i]);
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

        public override PythonMemberType ResultType {
            get {
                return PythonMemberType.Method;
            }
        }

        public override string ToString() {
            var name = _function.FunctionDefinition.Name;
            return "Method" /* + hex(id(self)) */ + " " + name;
        }
    }
}

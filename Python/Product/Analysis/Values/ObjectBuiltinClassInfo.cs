// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class ObjectBuiltinClassInfo : BuiltinClassInfo {
        private AnalysisValue _new;
        private AnalysisValue _setattr;

        public ObjectBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);

            switch (name) {
                case "__new__":
                    return _new = _new ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ObjectNew,
                        false
                    );
                case "__setattr__":
                    return _setattr = _setattr ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        ObjectSetAttr,
                        false
                    );
            }

            return res;
        }

        private IAnalysisSet ObjectNew(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 1) {
                var instance = AnalysisSet.Empty;
                foreach (var n in args[0]) {
                    var bci = n as BuiltinClassInfo;
                    var ci = n as ClassInfo;
                    if (bci != null) {
                        instance = instance.Union(bci.Instance);
                    } else if (ci != null) {
                        instance = instance.Union(ci.Instance);
                    } else if (n is LazyValueInfo lv) {
                        instance = instance.Union(LazyValueInfo.GetInstance(node, lv));
                    }
                }
                return instance;
            }
            return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;
        }

        private IAnalysisSet ObjectSetAttr(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 3) {
                foreach (var ii in args[0].Resolve(unit).OfType<InstanceInfo>()) {
                    foreach (var key in args[1].Resolve(unit).GetConstantValueAsString()) {
                        ii.SetMember(node, unit, key, args[2].Resolve(unit));
                    }
                }
            }
            return AnalysisSet.Empty;
        }

    }
}

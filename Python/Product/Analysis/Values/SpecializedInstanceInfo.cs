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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a mashup of one our specialized instances which does extend type tracking
    /// (e.g. list, dict, tuple) and a uesr defined type.  This gets created when a user 
    /// subclasses one of these types.  We create per-node specialized instances just like we
    /// do for the normal specialized instances.  When an operation on this object is processed
    /// we dispatch to any overridden methods and if there isn't an overridden method then
    /// we fall back to the specialized processing.
    /// 
    /// This class doesn't yet have everything specialized in it - we currently just support
    /// the oeprations which we have customized in our existing specialized implementations.
    /// </summary>
    class SpecializedInstanceInfo : InstanceInfo {
        internal IAnalysisSet _instances;

        public SpecializedInstanceInfo(ClassInfo klass, IAnalysisSet instances) : base(klass) {
            _instances = instances;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            IAnalysisSet res;
            if (TryInvokeMethod(node, unit, "__getitem__", new[] { index }, out res)) {
                return res;
            }

            return _instances.GetIndex(node, unit, index);
        }

        private bool TryInvokeMethod(Node node, AnalysisUnit unit, string name, IAnalysisSet[] args, out IAnalysisSet res) {
            res = AnalysisSet.Empty;
            bool invoked = false;
            var members = GetTypeMember(node, unit, name);
            foreach (var member in members) {
                if (ShouldInvokeMethod(member, name)) {
                    invoked = true;
                    res = res.Union(
                        member.Call(
                            node,
                            unit,
                            args,
                            ExpressionEvaluator.EmptyNames
                        )
                    );
                }
            }
            return invoked;
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            foreach (var instance in _instances) {
                foreach (var value in instance.GetItems()) {
                    yield return value;
                }
            }
            foreach (var value in base.GetItems()) {
                yield return value;
            }
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            IAnalysisSet res;
            if (!TryInvokeMethod(node, unit, "__setitem__", new[] { index }, out res)) {
                _instances.SetIndex(node, unit, index, value);
            }
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (Push()) {
                try {
                    IAnalysisSet res;
                    if (TryInvokeMethod(node, unit, "__iter__", Array.Empty<IAnalysisSet>(), out res)) {
                        if (res.Any()) {
                            return res
                                .GetMember(node, unit, unit.State.LanguageVersion.Is3x() ? "__next__" : "next")
                                .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);
                        }
                    }

                    if (TryInvokeMethod(node, unit, "__getitem__", new[] { unit.State.ClassInfos[BuiltinTypeId.Int].SelfSet }, out res)) {
                        return res;
                    }
                } finally {
                    Pop();
                }
            }

            return _instances.GetEnumeratorTypes(node, unit);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var members = base.GetMember(node, unit, name);
            IAnalysisSet res = AnalysisSet.Empty;
            bool found = false;
            foreach (var member in members) {
                if (ShouldInvokeMethod(member, name)) {
                    found = true;
                    res = res.Union(member.SelfSet);
                }
            }
            if (found) {
                return res;
            }

            return _instances.GetMember(node, unit, name);
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            string op = BinaryOpToString(operation);
            if (op != null) {
                IAnalysisSet res;
                if (TryInvokeMethod(node, unit, op, new[] { rhs }, out res)) {
                    return res;
                }
            }

            return _instances.BinaryOperation(
                node,
                unit,
                operation,
                rhs
            );
        }


        /// <summary>
        /// Checks to see if the member is the method that we've inherited from
        /// our specialized base class.  If it isn't then we should invoke it and
        /// use the result rather than using the information tracked in the 
        /// specialized value.
        /// </summary>
        private bool ShouldInvokeMethod(AnalysisValue member, string name) {
            BoundBuiltinMethodInfo method = member as BoundBuiltinMethodInfo;
            if (method == null) {
                // it's not a builtin function, the method is overridden
                return true;
            }

            var func = method.Method;
            if (func.Name != name) {
                // the function name is wrong, the user may have done something crazy like:
                // class C(dict):
                //      __getitem__ == some_other_builtin_func
                return true;
            }

            if (func.Function.DeclaringType != null) {
                foreach (var inst in _instances) {
                    if (inst.TypeId == func.Function.DeclaringType.TypeId) {
                        // the function is the original specialized function, not overridden
                        return false;
                    }
                }
            }

            // The name matches, but the function is from the wrong type.
            // The user must have done something like:
            // class C(dict):
            //      __getitem__ = list.__getitem__

            return true;
        }

    }
}

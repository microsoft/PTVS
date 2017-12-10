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
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class LazyValueInfo : AnalysisValue {
        private readonly Node _node;
        private readonly LazyValueInfo _left, _right;
        private readonly IAnalysisSet _value;
        private readonly string _memberName;
        private readonly PythonOperator? _op;

        protected LazyValueInfo(Node node) {
            _node = node;
        }

        public LazyValueInfo(Node node, IAnalysisSet value) {
            _node = node;
            _value = value;
        }

        private LazyValueInfo(Node node, LazyValueInfo target, string memberName) {
            _node = node;
            _left = target;
            _memberName = memberName;
        }

        private LazyValueInfo(Node node, PythonOperator op, LazyValueInfo right) {
            _node = node;
            _op = op;
            _right = right;
        }

        private LazyValueInfo(Node node, LazyValueInfo left, PythonOperator op, LazyValueInfo right) {
            _node = node;
            _left = left;
            _op = op;
            _right = right;
        }

        public virtual IAnalysisSet Resolve(AnalysisUnit unit, FunctionInfo calling, ArgumentSet callingArgs) {
            if (_value is ParameterInfo pi) {
                return pi.Resolve(unit, calling, callingArgs);
            } else if (_value != null) {
                return _value;
            }

            if (_memberName != null) {
                return _left.Resolve(unit, calling, callingArgs).GetMember(_node, unit, _memberName);
            }

            if (_op.HasValue) {
                if (_left == null) {
                    return _right.Resolve(unit, calling, callingArgs).UnaryOperation(_node, unit, _op.Value);
                }
                return _left.Resolve(unit, calling, callingArgs).BinaryOperation(_node, unit, _op.Value, _right.Resolve(unit, calling, callingArgs));
            }

            return AnalysisSet.Empty;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            return new LazyValueInfo(node, this, name);
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return new LazyValueInfo(node, this, operation, new LazyValueInfo(node, rhs));
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return new LazyValueInfo(node, operation, this);
        }
    }
}

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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    abstract class SpecializedNamespace : AnalysisValue {
        protected readonly AnalysisValue _original, _inst;
        private IAnalysisSet _descriptor;

        public SpecializedNamespace(AnalysisValue original) {
            _original = original;
        }

        public SpecializedNamespace(AnalysisValue original, AnalysisValue inst) {
            _original = original;
            _inst = inst;
        }

        internal AnalysisValue Original {
            get {
                return _original;
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (_original == null) {
                return base.Call(node, unit, args, keywordArgNames);
            }

            return _original.Call(node, unit, args, keywordArgNames);
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            if (_original == null) {
                base.AddReference(node, analysisUnit);
                return;
            }

            _original.AddReference(node, analysisUnit);
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            if (_original == null) {
                base.AugmentAssign(node, unit, value);
                return;
            }

            _original.AugmentAssign(node, unit, value);
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, IAnalysisSet rhs) {
            if (_original == null) {
                return base.BinaryOperation(node, unit, operation, rhs);
            }

            return _original.BinaryOperation(node, unit, operation, rhs);
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                if (_original == null) {
                    return base.DeclaringModule;
                }

                return _original.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                if (_original == null) {
                    return base.DeclaringVersion;
                }

                return _original.DeclaringVersion;
            }
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            if (_original == null) {
                base.DeleteMember(node, unit, name);
                return;
            }

            _original.DeleteMember(node, unit, name);
        }

        public override string Description {
            get {
                if (_original == null) {
                    return base.Description;
                }

                return _original.Description;
            }
        }

        public override string Documentation {
            get {
                if (_original == null) {
                    return base.Documentation;
                }

                return _original.Documentation;
            }
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (_original == null) {
                return base.GetAllMembers(moduleContext, options);
            }
            return _original.GetAllMembers(moduleContext, options);
        }

        public override object GetConstantValue() {
            if (_original == null) {
                return base.GetConstantValue();
            }

            return _original.GetConstantValue();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (_original == null) {
                return base.GetDescriptor(node, instance, context, unit);
            }

            if (_descriptor == null) {
                var res = _original.GetDescriptor(node, instance, context, unit);
                // TODO: This kinda sucks...
                if (Object.ReferenceEquals(res, _original)) {
                    _descriptor = SelfSet;
                } else if (res.Count >= 1) {
                    // TODO: Dictionary per-instance

                    _descriptor = Clone(res.First(), instance);
                } else {
                    _descriptor = Clone(_original, instance);
                }
            }
            return _descriptor;
        }

        protected abstract SpecializedNamespace Clone(AnalysisValue original, AnalysisValue instance);

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (_original == null) {
                return base.GetEnumeratorTypes(node, unit);
            }

            return _original.GetEnumeratorTypes(node, unit);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            if (_original == null) {
                return base.GetIndex(node, unit, index);
            }

            return _original.GetIndex(node, unit, index);
        }

        public override int? GetLength() {
            if (_original == null) {
                return base.GetLength();
            }

            return _original.GetLength();
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            if (_original == null) {
                return AnalysisSet.Empty;
            }

            return _original.GetTypeMember(node, unit, name);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (_original == null) {
                return AnalysisSet.Empty;
            }

            return _original.GetMember(node, unit, name);
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            if (_original == null) {
                return false;
            }
            return _original.IsOfType(klass);
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                if (_original == null) {
                    return new LocationInfo[0];
                }
                return _original.Locations;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_original == null) {
                    return new OverloadResult[0];
                }
                return _original.Overloads;
            }
        }

        public override IPythonType PythonType {
            get {
                if (_original == null) {
                    return null;
                }
                return _original.PythonType;
            }
        }

        internal override IEnumerable<LocationInfo> References {
            get {
                if (_original == null) {
                    return new LocationInfo[0];
                }
                return _original.References;
            }
        }

        public override PythonMemberType MemberType {
            get {
                if (_original == null) {
                    return PythonMemberType.Unknown;
                }
                return _original.MemberType;
            }
        }

        public override IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, IAnalysisSet rhs) {
            if (_original == null) {
                return AnalysisSet.Empty;
            }
            return _original.ReverseBinaryOperation(node, unit, operation, rhs);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            if (_original != null) {
                _original.SetIndex(node, unit, index, value);
            }
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            if (_original != null) {
                _original.SetMember(node, unit, name, value);
            }
        }

        public override string ShortDescription {
            get {
                if (_original == null) {
                    return string.Empty;
                }
                return _original.ShortDescription;
            }
        }

        internal override BuiltinTypeId TypeId {
            get {
                if (_original == null) {
                    return BuiltinTypeId.Unknown;
                }
                return _original.TypeId;
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation) {
            if (_original == null) {
                return AnalysisSet.Empty;
            }
            return _original.UnaryOperation(node, unit, operation);
        }
    }
}

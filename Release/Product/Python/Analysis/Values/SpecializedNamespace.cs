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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    abstract class SpecializedNamespace : Namespace {
        protected readonly Namespace _original, _inst;
        private ISet<Namespace> _descriptor;

        public SpecializedNamespace(Namespace original) {
            _original = original;
        }

        public SpecializedNamespace(Namespace original, Namespace inst) {
            _original = original;
            _inst = inst;
        }

        internal Namespace Original {
            get {
                return _original;
            }
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            return _original.Call(node, unit, args, keywordArgNames);
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            _original.AddReference(node, analysisUnit);
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            _original.AugmentAssign(node, unit, value);
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, ISet<Namespace> rhs) {
            return _original.BinaryOperation(node, unit, operation, rhs);
        }

        public override ProjectEntry DeclaringModule {
            get {
                return _original.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _original.DeclaringVersion;
            }
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            _original.DeleteMember(node, unit, name);
        }

        public override string Description {
            get {
                return _original.Description;
            }
        }

        public override string Documentation {
            get {
                return _original.Documentation;
            }
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return _original.GetAllMembers(moduleContext);
        }

        public override object GetConstantValue() {
            return _original.GetConstantValue();
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
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

        protected abstract SpecializedNamespace Clone(Namespace original, Namespace instance);

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _original.GetEnumeratorTypes(node, unit);
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return _original.GetIndex(node, unit, index);
        }

        public override int? GetLength() {
            return _original.GetLength();
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return _original.GetMember(node, unit, name);
        }

        public override ISet<Namespace> GetStaticDescriptor(AnalysisUnit unit) {
            return _original.GetStaticDescriptor(unit);
        }

        public override bool IsOfType(BuiltinClassInfo klass) {
            return _original.IsOfType(klass);
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                return _original.Locations;
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                return _original.Overloads;
            }
        }

        public override IPythonType PythonType {
            get {
                return _original.PythonType;
            }
        }

        public override IEnumerable<LocationInfo> References {
            get {
                return _original.References;
            }
        }

        public override PythonMemberType ResultType {
            get {
                return _original.ResultType;
            }
        }

        public override ISet<Namespace> ReverseBinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, ISet<Namespace> rhs) {
            return _original.ReverseBinaryOperation(node, unit, operation, rhs);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            _original.SetIndex(node, unit, index, value);
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            _original.SetMember(node, unit, name, value);
        }

        public override string ShortDescription {
            get {
                return _original.ShortDescription;
            }
        }

        public override BuiltinTypeId TypeId {
            get {
                return _original.TypeId;
            }
        }

        public override ISet<Namespace> UnaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation) {
            return _original.UnaryOperation(node, unit, operation);
        }
    }
}

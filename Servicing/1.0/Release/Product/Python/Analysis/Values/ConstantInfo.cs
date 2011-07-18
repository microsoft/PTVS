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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ConstantInfo : BuiltinInstanceInfo {
        private readonly object _value;
        private readonly Namespace _builtinInfo;
        private readonly PythonMemberType _memberType;
        private string _doc;

        public ConstantInfo(object value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetNamespaceFromObjects(projectState.GetTypeFromObject(value))) {
            _value = value;
            _memberType = PythonMemberType.Constant;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetNamespaceFromObjects(_type)).Instance;
        }

        public ConstantInfo(IPythonConstant value, PythonAnalyzer projectState)
            : base((BuiltinClassInfo)projectState.GetNamespaceFromObjects(value.Type)) {
            _value = value;
            _memberType = value.MemberType;
            _builtinInfo = ((BuiltinClassInfo)projectState.GetNamespaceFromObjects(value.Type)).Instance;
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            return _builtinInfo.BinaryOperation(node, unit, operation, rhs);
        }

        public override ISet<Namespace> UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return _builtinInfo.UnaryOperation(node, unit, operation);
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            return _builtinInfo.Call(node, unit, args, keywordArgNames);
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            _builtinInfo.AugmentAssign(node, unit, value);
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            return _builtinInfo.GetDescriptor(node, instance, context, unit);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return _builtinInfo.GetMember(node, unit, name);
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            _builtinInfo.SetMember(node, unit, name, value);
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return base.GetIndex(node, unit, index);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            _builtinInfo.SetIndex(node, unit, index, value);
        }

        public override ISet<Namespace> GetStaticDescriptor(AnalysisUnit unit) {
            return _builtinInfo.GetStaticDescriptor(unit);
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return _builtinInfo.GetAllMembers(moduleContext);
        }

        public override string Description {
            get {
                if (_value == null) {
                    return "None";
                }

                return _type.Name;
                //return PythonOps.Repr(ProjectState.CodeContext, _value);
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    object docObj = _type.Documentation;
                    _doc = docObj == null ? "" : Utils.StripDocumentation(docObj.ToString());
                }
                return _doc;
            }
        }

        public override PythonMemberType ResultType {
            get {
                return _memberType;
            }
        }

        public override string ToString() {
            return "<ConstantInfo object '" + Description + "'>"; // " at " + hex(id(self))
        }

        public override object GetConstantValue() {
            return _value;
        }

        public override bool UnionEquals(Namespace ns) {
            ConstantInfo ci = ns as ConstantInfo;
            if (ci == null) {
                return false;
            } else if (ci._value == _value) {
                return true;
            } else if (_value == null) {
                return false;
            }

            return _value.GetType() == ci._value.GetType();
        }

        public override int UnionHashCode() {
            if (_value == null) {
                return 0;
            }

            return _value.GetType().GetHashCode();
        }

        public object Value {
            get {
                return _value;
            }
        }
    }
}

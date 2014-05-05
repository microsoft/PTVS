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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    sealed class MultipleMemberInfo : AnalysisValue, IModule {
        private readonly AnalysisValue[] _members;

        public static IAnalysisSet Create(params IEnumerable<AnalysisValue>[] members) {
            var allMembers = members.SelectMany().Where(m => m != null).ToArray();
            if (allMembers.Length == 0) {
                return AnalysisSet.Empty;
            } else if (allMembers.Length == 1) {
                return allMembers[0];
            } else {
                return new MultipleMemberInfo(allMembers);
            }
        }

        private MultipleMemberInfo(AnalysisValue[] members) {
            _members = members;
        }

        public AnalysisValue[] Members {
            get {
                return _members;
            }
        }

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Multiple; }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                return _members.SelectMany(m => m.Overloads).Distinct(OverloadResultComparer.Instance);
            }
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var ignored = base.GetMember(node, unit, name);

            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetMember(node, unit, name));
            }
            return res;
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            foreach (var member in _members) {
                member.AugmentAssign(node, unit, value);
            }
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.BinaryOperation(node, unit, operation, rhs));
            }
            return res;
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.Call(node, unit, args, keywordArgNames));
            }
            return res;
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            foreach (var member in _members) {
                member.DeleteMember(node, unit, name);
            }
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(PythonTools.Interpreter.IModuleContext moduleContext) {
            Dictionary<string, IAnalysisSet> res = new Dictionary<string, IAnalysisSet>();
            foreach (var member in _members) {
                foreach (var keyValue in member.GetAllMembers(moduleContext)) {
                    IAnalysisSet existing;
                    if (res.TryGetValue(keyValue.Key, out existing)) {
                        MultipleMemberInfo existingMultiMember = existing as MultipleMemberInfo;
                        if (existingMultiMember != null) {
                            res[keyValue.Key] = MultipleMemberInfo.Create(existingMultiMember._members, keyValue.Value);
                        } else {
                            res[keyValue.Key] = MultipleMemberInfo.Create(existing, keyValue.Value);
                        }
                    } else {
                        res[keyValue.Key] = keyValue.Value;
                    }
                }
            }

            return res;
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetDescriptor(node, instance, context, unit));
            }
            return res;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetIndex(node, unit, index));
            }
            return res;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            foreach (var member in _members) {
                member.SetIndex(node, unit, index, value);
            }
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetEnumeratorTypes(node, unit));
            }
            return res;
        }

        public override object GetConstantValue() {
            foreach (var member in _members) {
                object res = member.GetConstantValue();
                if (res != Type.Missing) {
                    return res;
                }
            }
            return base.GetConstantValue();
        }

        public override IAnalysisSet GetStaticDescriptor(AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetStaticDescriptor(unit));
            }
            return res;
        }

        public override int? GetLength() {
            foreach (var member in _members) {
                var res = member.GetLength();
                if (res != null) {
                    return res;
                }
            }
            return null;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            foreach (var member in _members) {
                member.SetMember(node, unit, name, value);
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.UnaryOperation(node, unit, operation));
            }
            return res;
        }

        public override string Documentation {
            get {
                StringBuilder res = new StringBuilder();
                HashSet<string> docs = new HashSet<string>();
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.Documentation) && !docs.Contains(member.Documentation)) {
                        docs.Add(member.Documentation);
                        res.AppendLine(member.Documentation);
                        res.AppendLine();
                    }
                }
                return res.ToString();
            }
        }

        public override string Description {
            get {
                StringBuilder res = new StringBuilder();
                HashSet<string> descs = new HashSet<string>();
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.Description) && !descs.Contains(member.Description)) {
                        descs.Add(member.Description);
                        res.AppendLine(member.Description);
                        res.AppendLine();
                    }
                }
                return res.ToString();
            }
        }

        public override string ShortDescription {
            get {
                StringBuilder res = new StringBuilder();
                HashSet<string> descs = new HashSet<string>();
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.ShortDescription) && !descs.Contains(member.ShortDescription)) {
                        if (res.Length != 0) {
                            res.Append(", ");
                        }
                        res.Append(member.ShortDescription);
                        descs.Add(member.ShortDescription);
                    }
                }
                return res.ToString();
            }
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                return _members.SelectMany(m => m.Locations);
            }
        }

        IModule IModule.GetChildPackage(IModuleContext context, string name) {
            var children = new List<AnalysisValue>();
            foreach (var member in _members.OfType<IModule>()) {
                var mod = member.GetChildPackage(context, name) as AnalysisValue;
                if (mod != null) {
                    children.Add(mod);
                }
            }

            return MultipleMemberInfo.Create(children) as IModule;
        }

        IEnumerable<KeyValuePair<string, AnalysisValue>> IModule.GetChildrenPackages(IModuleContext context) {
            foreach (var member in _members.OfType<IModule>()) {
                foreach (var keyValue in member.GetChildrenPackages(context)) {
                    yield return keyValue;
                }
            }
        }

        void IModule.SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            foreach (var member in _members.OfType<IModule>()) {
                member.SpecializeFunction(name, callable, mergeOriginalAnalysis);
            }
        }

        IEnumerable<string> IModule.GetModuleMemberNames(IModuleContext context) {
            foreach (var member in _members.OfType<IModule>()) {
                foreach (var name in member.GetModuleMemberNames(context)) {
                    yield return name;
                }
            }
        }

        IAnalysisSet IModule.GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef, InterpreterScope linkedScope, string linkedName) {
            var res = AnalysisSet.Empty;
            foreach (var member in _members.OfType<IModule>()) {
                res = res.Union(member.GetModuleMember(node, unit, name, addRef, linkedScope, linkedName));
            }
            return res;
        }

        void IModule.Imported(AnalysisUnit unit) {
            foreach (var member in _members.OfType<IModule>()) {
                member.Imported(unit);
            }
        }

        public override bool Equals(object obj) {
            var other = obj as MultipleMemberInfo;
            return other != null && !_members.Except(other._members).Any();
        }

        public override int GetHashCode() {
            return _members.Aggregate(6451, (a, m) => a ^ m.GetHashCode());
        }
    }
}

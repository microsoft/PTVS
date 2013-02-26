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
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    sealed class MultipleMemberInfo : Namespace, IModule {
        private readonly Namespace[] _members;

        public MultipleMemberInfo(Namespace[] members) {
            _members = members;
        }

        public Namespace[] Members {
            get {
                return _members;
            }
        }

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Multiple; }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                List<OverloadResult> res = new List<OverloadResult>();
                foreach (var member in _members) {
                    AppendOverloads(res, member.Overloads);
                }
                return res.ToArray();
            }
        }

        /// <summary>
        /// Appends the overloads and avoids adding duplicates.
        /// </summary>
        internal static void AppendOverloads(List<OverloadResult> appendTo, IEnumerable<OverloadResult> newOverloads) {
            bool contains = false;
            foreach (var overload in newOverloads) {
                for (int i = 0; i < appendTo.Count; i++) {
                    if (appendTo[i].Name == overload.Name &&
                        appendTo[i].Documentation == overload.Documentation &&
                        appendTo[i].Parameters.Length == overload.Parameters.Length) {
                        bool differParams = false;
                        for (int j = 0; j < overload.Parameters.Length; j++) {
                            if (overload.Parameters[j].DefaultValue != appendTo[i].Parameters[j].DefaultValue ||
                                overload.Parameters[j].Documentation != appendTo[i].Parameters[j].Documentation ||
                                overload.Parameters[j].IsOptional != appendTo[i].Parameters[j].IsOptional ||
                                overload.Parameters[j].Name != appendTo[i].Parameters[j].Name ||
                                overload.Parameters[j].Type != appendTo[i].Parameters[j].Type) {
                                differParams = true;
                                break;
                            }
                        }

                        if (!differParams) {
                            contains = true;
                            break;
                        }
                    }
                }

                if (!contains) {
                    appendTo.Add(overload);
                }
            }
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = NamespaceSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetMember(node, unit, name));
            }
            return res;
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, INamespaceSet value) {
            foreach (var member in _members) {
                member.AugmentAssign(node, unit, value);
            }
        }

        public override INamespaceSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            var res = NamespaceSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.BinaryOperation(node, unit, operation, rhs));
            }
            return res;
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            var res = NamespaceSet.Empty;
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

        public override IDictionary<string, INamespaceSet> GetAllMembers(PythonTools.Interpreter.IModuleContext moduleContext) {
            Dictionary<string, INamespaceSet> res = new Dictionary<string, INamespaceSet>();
            foreach (var mem in _members) {
                foreach (var keyValue in mem.GetAllMembers(moduleContext)) {
                    INamespaceSet existing;
                    if (res.TryGetValue(keyValue.Key, out existing)) {
                        MultipleMemberInfo existingMultiMember = existing as MultipleMemberInfo;
                        if (existingMultiMember != null) {
                            res[keyValue.Key] = new MultipleMemberInfo(existingMultiMember._members.Concat(keyValue.Value).ToArray());
                        } else {
                            res[keyValue.Key] = new MultipleMemberInfo(existing.Concat(keyValue.Value).ToArray());
                        }
                    } else {
                        res[keyValue.Key] = keyValue.Value;
                    }
                }
            }

            return res;
        }

        public override INamespaceSet GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetDescriptor(node, instance, context, unit));
            }
            return res;
        }

        public override INamespaceSet GetIndex(Node node, AnalysisUnit unit, INamespaceSet index) {
            var res = NamespaceSet.Empty;
            foreach (var member in _members) {
                res = res.Union(member.GetIndex(node, unit, index));
            }
            return res;
        }

        public override void SetIndex(Node node, AnalysisUnit unit, INamespaceSet index, INamespaceSet value) {
            foreach (var member in _members) {
                member.SetIndex(node, unit, index, value);
            }
        }

        public override INamespaceSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
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

        public override INamespaceSet GetStaticDescriptor(AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
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

        public override void SetMember(Node node, AnalysisUnit unit, string name, INamespaceSet value) {
            foreach (var member in _members) {
                member.SetMember(node, unit, name, value);
            }
        }

        public override INamespaceSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            var res = NamespaceSet.Empty;
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
                foreach (var member in _members) {
                    foreach (var loc in member.Locations) {
                        yield return loc;
                    }
                }
            }
        }

        IModule IModule.GetChildPackage(IModuleContext context, string name) {
            var children = new List<Namespace>();
            foreach (var member in _members.OfType<IModule>()) {
                var mod = member.GetChildPackage(context, name) as Namespace;
                if (mod != null) {
                    children.Add(mod);
                }
            }

            if (children.Count == 0) {
                return null;
            } else if (children.Count == 1) {
                return (IModule)children[0];
            } else {
                return (IModule)new MultipleMemberInfo(children.ToArray());
            }
        }

        IEnumerable<KeyValuePair<string, Namespace>> IModule.GetChildrenPackages(IModuleContext context) {
            foreach (var member in _members.OfType<IModule>()) {
                foreach (var keyValue in member.GetChildrenPackages(context)) {
                    yield return keyValue;
                }
            }
        }

        void IModule.SpecializeFunction(string name, Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> dlg, bool analyze) {
            foreach (var member in _members.OfType<IModule>()) {
                member.SpecializeFunction(name, dlg, analyze);
            }
        }

        IEnumerable<string> IModule.GetModuleMemberNames(IModuleContext context) {
            foreach (var member in _members.OfType<IModule>()) {
                foreach (var name in member.GetModuleMemberNames(context)) {
                    yield return name;
                }
            }
        }

        INamespaceSet IModule.GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef, InterpreterScope linkedScope, string linkedName) {
            var res = NamespaceSet.Empty;
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
    }
}

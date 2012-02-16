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
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    sealed class MultipleMemberInfo : Namespace {
        private readonly Namespace[] _members;

        public MultipleMemberInfo(Namespace[] members) {
            _members = members;
        }

        public Namespace[] Members {
            get {
                return _members;
            }
        }

        public override PythonMemberType ResultType {
            get { return PythonMemberType.Multiple; }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                List<OverloadResult> res = new List<OverloadResult>();
                foreach (var member in _members) {
                    res.AddRange(member.Overloads);
                }
                return res.ToArray();
            }
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.GetMember(node, unit, name), ref madeSet);
            }
            return res;
        }

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            foreach (var member in _members) {
                member.AugmentAssign(node, unit, value);
            } 
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.BinaryOperation(node, unit, operation, rhs), ref madeSet);
            }
            return res;
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.Call(node, unit, args, keywordArgNames), ref madeSet);
            }
            return res;
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            foreach (var member in _members) {
                member.DeleteMember(node, unit, name);
            } 
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(PythonTools.Interpreter.IModuleContext moduleContext) {
            Dictionary<string, ISet<Namespace>> res = new Dictionary<string, ISet<Namespace>>();
            foreach(var mem in _members) {
                foreach (var keyValue in mem.GetAllMembers(moduleContext)) {
                    res[keyValue.Key] = keyValue.Value;
                }
            }

            return res;
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.GetDescriptor(node, instance, context, unit), ref madeSet);
            }
            return res;
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.GetIndex(node, unit, index), ref madeSet);
            }
            return res;
        }
        
        public override void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            foreach (var member in _members) {
                member.SetIndex(node, unit, index, value);
            }            
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.GetEnumeratorTypes(node, unit), ref madeSet);
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

        public override ISet<Namespace> GetStaticDescriptor(AnalysisUnit unit) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.GetStaticDescriptor(unit), ref madeSet);
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

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            foreach (var member in _members) {
                member.SetMember(node, unit, name, value);
            } 
        }

        public override ISet<Namespace> UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var member in _members) {
                res = res.Union(member.UnaryOperation(node, unit, operation), ref madeSet);
            }
            return res;
        }

        public override string Documentation {
            get {
                StringBuilder res = new StringBuilder();
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.Documentation)) {
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
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.Description)) {
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
                foreach (var member in _members) {
                    if (!String.IsNullOrWhiteSpace(member.ShortDescription)) {
                        if (res.Length != 0) {
                            res.Append(", ");
                        }
                        res.Append(member.ShortDescription);
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
    }
}

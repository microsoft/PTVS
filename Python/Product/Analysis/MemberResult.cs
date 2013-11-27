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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct MemberResult {
        private readonly string _name;
        private string _completion;
        private readonly Func<IEnumerable<AnalysisValue>> _vars;
        private readonly Func<PythonMemberType> _type;

        internal MemberResult(string name, IEnumerable<AnalysisValue> vars) {
            _name = _completion = name;
            _vars = () => vars;
            _type = null;
            _type = GetMemberType;
        }

        public MemberResult(string name, PythonMemberType type) {
            _name = _completion = name;
            _type = () => type;
            _vars = () => Empty;
        }

        internal MemberResult(string name, string completion, IEnumerable<AnalysisValue> vars, PythonMemberType? type) {
            _name = name;
            _vars = () => vars;
            _completion = completion;
            if (type != null) {
                _type = () => type.Value;
            } else {
                _type = null;
                _type = GetMemberType;
            }
        }

        internal MemberResult(string name, Func<IEnumerable<AnalysisValue>> vars, Func<PythonMemberType> type) {
            _name = _completion = name;
            _vars = vars;
            _type = type;
        }

        public MemberResult FilterCompletion(string completion) {
            return new MemberResult(Name, completion, Namespaces, MemberType);
        }

        private static AnalysisValue[] Empty = new AnalysisValue[0];

        public string Name {
            get { return _name; }
        }

        public string Completion {
            get { return _completion; }
        }

        public string Documentation {
            get {
                var docSeen = new HashSet<string>();
                var typeSeen = new HashSet<string>();
                var docs = new List<string>();
                var types = new List<string>();

                var doc = new StringBuilder();

                foreach (var ns in _vars()) {
                    var docString = ns.Documentation;
                    if (docSeen.Add(docString)) {
                        docs.Add(docString);
                    }
                    var typeString = ns.ShortDescription;
                    if (typeSeen.Add(typeString)) {
                        types.Add(typeString);
                    }
                }

                var mt = MemberType;
                if (mt == PythonMemberType.Instance || mt == PythonMemberType.Constant) {
                    switch (mt) {
                        case PythonMemberType.Instance:
                            doc.Append("Instance of ");
                            break;
                        case PythonMemberType.Constant:
                            doc.Append("Constant ");
                            break;
                        default:
                            doc.Append("Value of ");
                            break;
                    }
                    if (types.Count == 0) {
                        doc.AppendLine("unknown type");
                    } else if (types.Count == 1) {
                        doc.AppendLine(types[0]);
                    } else {
                        var orStr = types.Count == 2 ? " or " : ", or ";
                        doc.AppendLine(string.Join(", ", types.Take(types.Count - 1)) + orStr + types.Last());
                    }
                    doc.AppendLine();
                }
                foreach (var str in docs.OrderBy(s => s)) {
                    doc.AppendLine(str);
                    doc.AppendLine();
                }
                return Utils.CleanDocumentation(doc.ToString());
            }
        }

        public PythonMemberType MemberType {
            get {
                return _type();
            }
        }

        private PythonMemberType GetMemberType() {
            bool includesNone = false;
            PythonMemberType result = PythonMemberType.Unknown;

            var allVars = _vars().SelectMany(ns => {
                var mmi = ns as MultipleMemberInfo;
                if (mmi != null) {
                    return mmi.Members;
                } else {
                    return Enumerable.Repeat(ns, 1);
                }
            });

            foreach (var ns in allVars) {
                var nsType = ns.MemberType;
                if (ns.TypeId == BuiltinTypeId.NoneType) {
                    includesNone = true;
                } else if (result == PythonMemberType.Unknown) {
                    result = nsType;
                } else if (result == nsType) {
                    // No change
                } else if (result == PythonMemberType.Constant && nsType == PythonMemberType.Instance) {
                    // Promote from Constant to Instance
                    result = PythonMemberType.Instance;
                } else {
                    return PythonMemberType.Multiple;
                }
            }
            if (result == PythonMemberType.Unknown) {
                return includesNone ? PythonMemberType.Constant : PythonMemberType.Instance;
            }
            return result;
        }

        internal IEnumerable<AnalysisValue> Namespaces {
            get {
                return _vars();
            }
        }

        /// <summary>
        /// Gets the location(s) for the member(s) if they are available.
        /// 
        /// New in 1.5.
        /// </summary>
        public IEnumerable<LocationInfo> Locations {
            get {
                foreach (var ns in _vars()) {
                    foreach (var location in ns.Locations) {
                        yield return location;
                    }
                }
            }
        }

        public override bool Equals(object obj) {
            if (!(obj is MemberResult)) {
                return false;
            }

            return Name == ((MemberResult)obj).Name;
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}

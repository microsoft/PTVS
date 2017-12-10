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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct MemberResult {
        private readonly string _name;
        private string _completion;
        private readonly Lazy<IEnumerable<AnalysisValue>> _vars;
        private readonly Lazy<PythonMemberType> _type;

        private static readonly Lazy<PythonMemberType> UnknownType =
            new Lazy<PythonMemberType>(() => PythonMemberType.Unknown);
        private static readonly Lazy<IEnumerable<AnalysisValue>> EmptyValues =
            new Lazy<IEnumerable<AnalysisValue>>(Enumerable.Empty<AnalysisValue>);

        internal MemberResult(string name, IEnumerable<AnalysisValue> vars) {
            _name = _completion = name;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            _type = UnknownType;
            _type = new Lazy<PythonMemberType>(GetMemberType);
        }

        public MemberResult(string name, PythonMemberType type) {
            _name = _completion = name;
            _type = new Lazy<PythonMemberType>(() => type);
            _vars = EmptyValues;
        }

        public MemberResult(string name, string completion, IEnumerable<AnalysisValue> vars, PythonMemberType? type) {
            _name = name;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            _completion = completion;
            _type = UnknownType;
            if (type != null) {
                _type = new Lazy<PythonMemberType>(() => type.Value);
            } else {
                _type = new Lazy<PythonMemberType>(GetMemberType);
            }
        }

        internal MemberResult(string name, Func<IEnumerable<AnalysisValue>> vars, Func<PythonMemberType> type) {
            _name = _completion = name;
            _vars = vars == null ? EmptyValues : new Lazy<IEnumerable<AnalysisValue>>(vars);
            _type = type == null ? UnknownType : new Lazy<PythonMemberType>(type);
        }

        public MemberResult FilterCompletion(string completion) {
            return new MemberResult(Name, completion, Values, MemberType);
        }

        public string Name {
            get { return _name; }
        }

        public string Completion {
            get { return _completion; }
        }

        private static string GetDescription(AnalysisValue ns) {
            var d = ns?.ShortDescription;
            if (string.IsNullOrEmpty(d)) {
                return null;
            }
            switch (ns.MemberType) {
                case PythonMemberType.Instance:
                    return "instance of " + d;
                case PythonMemberType.Constant:
                    return "constant " + d;
            }
            return d;
        }

        private static IEnumerable<AnalysisValue> SeparateMultipleMembers(IEnumerable<AnalysisValue> values) {
            foreach (var v in values) {
                if (v is MultipleMemberInfo mm) {
                    foreach (var m in mm.Members) {
                        yield return m;
                    }
                } else {
                    yield return v;
                }
            }
        }

        public string Documentation {
            get {
                var docs = new Dictionary<string, HashSet<string>>();
                var allTypes = new HashSet<string>();

                foreach (var ns in SeparateMultipleMembers(Values)) {
                    var docString = ns.Documentation?.TrimDocumentation();
                    var typeString = GetDescription(ns);
                    if (string.IsNullOrEmpty(docString)) {
                        docString = "";
                    }
                    if (!docs.TryGetValue(docString, out var docTypes)) {
                        docs[docString] = docTypes = new HashSet<string>();
                    }
                    if (!string.IsNullOrEmpty(typeString)) {
                        docTypes.Add(typeString);
                        allTypes.Add(typeString);
                    }
                }

                var doc = new StringBuilder();

                if (allTypes.Count == 0) {
                    return "unknown type";
                } else if (allTypes.Count == 1) {
                    doc.AppendLine(allTypes.First());
                    doc.AppendLine();
                } else {
                    var types = allTypes.OrderBy(s => s).ToList();
                    var orStr = types.Count == 2 ? " or " : ", or ";
                    doc.AppendLine(string.Join(", ", types.Take(types.Count - 1)) + orStr + types.Last());
                    doc.AppendLine();
                }

                var typeToDoc = new Dictionary<string, string>();
                foreach (var docType in docs) {
                    if (string.IsNullOrEmpty(docType.Key)) {
                        continue;
                    }

                    string typeDisplay = "unknown type";
                    var types = docType.Value.OrderBy(s => s).ToList();
                    if (types.Count <= 1) {
                        typeDisplay = "";
                    } else {
                        var orStr = types.Count == 2 ? " or " : ", or ";
                        typeDisplay = string.Join(", ", types.Take(types.Count - 1)) + orStr + types.Last() + ": ";
                    }
                    typeToDoc[typeDisplay] = docType.Key;
                }

                foreach (var typeDoc in typeToDoc.OrderBy(kv => kv.Key)) {
                    doc.AppendLine(typeDoc.Key + typeDoc.Value);
                    doc.AppendLine();
                }

                return Utils.CleanDocumentation(doc.ToString());
            }
        }

        public PythonMemberType MemberType => _type.Value;

        private PythonMemberType GetMemberType() {
            bool includesNone = false;
            PythonMemberType result = PythonMemberType.Unknown;

            var allVars = Values.SelectMany(ns => {
                var mmi = ns as MultipleMemberInfo;
                if (mmi != null) {
                    return mmi.Members;
                } else {
                    return Enumerable.Repeat(ns, 1);
                }
            });

            foreach (var ns in allVars.MaybeEnumerate()) {
                if (ns == null) {
                    Debug.Fail("Unexpected null AnalysisValue");
                    continue;
                }

                var nsType = ns.MemberType;

                var ci = ns as ConstantInfo;
                if (ci != null) {
                    if (ci.ClassInfo == ci.ProjectState.ClassInfos[BuiltinTypeId.Function]) {
                        nsType = PythonMemberType.Function;
                    } else if (ci.ClassInfo == ci.ProjectState.ClassInfos[BuiltinTypeId.Type]) {
                        nsType = PythonMemberType.Class;
                    } else if (ci.ClassInfo == ci.ProjectState.ClassInfos[BuiltinTypeId.Module]) {
                        nsType = PythonMemberType.Module;
                    }
                }

                if (ns.TypeId == BuiltinTypeId.NoneType) {
                    includesNone = true;
                } else if (result == PythonMemberType.Unknown) {
                    result = nsType;
                } else if (result == nsType) {
                    // No change
                } else if (result == PythonMemberType.Constant && nsType == PythonMemberType.Instance) {
                    // Promote from Constant to Instance
                    result = PythonMemberType.Instance;
                } else if (nsType == PythonMemberType.Unknown) {
                    // No change
                } else {
                    return PythonMemberType.Multiple;
                }
            }
            if (result == PythonMemberType.Unknown) {
                return includesNone ? PythonMemberType.Constant : PythonMemberType.Instance;
            }
            return result;
        }

        internal IEnumerable<AnalysisValue> Values => _vars.Value;

        /// <summary>
        /// Gets the location(s) for the member(s) if they are available.
        /// 
        /// New in 1.5.
        /// </summary>
        public IEnumerable<LocationInfo> Locations => Values.SelectMany(ns => ns.Locations);

        public override bool Equals(object obj) {
            if (!(obj is MemberResult)) {
                return false;
            }

            return Name == ((MemberResult)obj).Name;
        }

        public static bool operator ==(MemberResult x, MemberResult y) {
            return x.Name == y.Name;
        }

        public static bool operator !=(MemberResult x, MemberResult y) {
            return x.Name != y.Name;
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}

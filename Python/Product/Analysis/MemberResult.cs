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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct MemberResult {
        private readonly Lazy<IEnumerable<AnalysisValue>> _vars;
        private readonly Lazy<PythonMemberType> _type;

        private static readonly Lazy<PythonMemberType> UnknownType =
            new Lazy<PythonMemberType>(() => PythonMemberType.Unknown);
        private static readonly Lazy<IEnumerable<AnalysisValue>> EmptyValues =
            new Lazy<IEnumerable<AnalysisValue>>(Enumerable.Empty<AnalysisValue>);

        #region Constructors
        internal MemberResult(string name, IEnumerable<AnalysisValue> vars) {
            Name = Completion = name;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            _type = UnknownType;
            _type = new Lazy<PythonMemberType>(GetMemberType);
        }

        public MemberResult(string name, PythonMemberType type) {
            Name = Completion = name;
            _type = new Lazy<PythonMemberType>(() => type);
            _vars = EmptyValues;
        }

        public MemberResult(string name, string completion, IEnumerable<AnalysisValue> vars, PythonMemberType? type) {
            Name = name;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            Completion = completion;
            _type = UnknownType;
            if (type != null) {
                _type = new Lazy<PythonMemberType>(() => type.Value);
            } else {
                _type = new Lazy<PythonMemberType>(GetMemberType);
            }
        }

        internal MemberResult(string name, Func<IEnumerable<AnalysisValue>> vars, Func<PythonMemberType> type) {
            Name = Completion = name;
            _vars = vars == null ? EmptyValues : new Lazy<IEnumerable<AnalysisValue>>(vars);
            _type = type == null ? UnknownType : new Lazy<PythonMemberType>(type);
        }
        #endregion

        public MemberResult FilterCompletion(string completion) {
            return new MemberResult(Name, completion, Values, MemberType);
        }

        public string Name { get; }
        public string Completion { get; }

        /// <summary>
        /// Gets the location(s) for the member(s) if they are available.
        /// 
        /// New in 1.5.
        /// </summary>
        public IEnumerable<LocationInfo> Locations => Values.SelectMany(ns => ns.Locations);

        internal IEnumerable<AnalysisValue> Values => _vars.Value;

        public string Documentation {
            get {
                var docs = new Dictionary<string, HashSet<string>>();

                foreach (var ns in SeparateMultipleMembers(Values)) {
                    var docString = GetDocumentation(ns);
                    var typeString = GetDescription(ns);

                    // If first line of doc is already in the type string, then filter it out.
                    // This is because some functions have signature as a first doc line and 
                    // some do not have one. We are already showing signature as part of the type.
                    var lines = docString.Split('\n');
                    if (!string.IsNullOrEmpty(docString) && typeString != null && lines.Length > 1 && typeString.IndexOf(lines[0].Trim()) >= 0) {
                        docString = docString.Substring(lines[0].Length).TrimStart();
                    }

                    if (!docs.TryGetValue(docString, out var docTypes)) {
                        docs[docString] = docTypes = new HashSet<string>();
                    }
                    if (!string.IsNullOrEmpty(typeString)) {
                        docTypes.Add(typeString);
                    }
                }

                var doc = new StringBuilder();
                var typeToDoc = new Dictionary<string, Tuple<string, string>>();
                foreach (var docType in docs) {
                    if (!docType.Value.Any()) {
                        continue;
                    }

                    var typeDisplay = "unknown type";
                    var types = docType.Value.OrderBy(s => s).ToList();
                    if (types.Count == 1) {
                        typeDisplay = types[0];
                    } else {
                        var orStr = types.Count == 2 ? " or " : ", or ";
                        typeDisplay = string.Join(", ", types.Take(types.Count - 1)) + orStr + types.Last();
                    }
                    typeToDoc[string.Join(",", types)] = new Tuple<string, string>(typeDisplay, docType.Key);
                }

                foreach (var typeDoc in typeToDoc.OrderBy(kv => kv.Key)) {
                    doc.Append(typeDoc.Value.Item1);
                    var details = typeDoc.Value.Item2;
                    if (!string.IsNullOrEmpty(details)) {
                        doc.AppendLine(":");
                        doc.Append(details);
                    }
                    doc.AppendLine();
                    doc.AppendLine();
                }

                return doc.ToString().Trim();
            }
        }

        private static string GetDocumentation(AnalysisValue ns) {
            var doc = ns.Documentation?.TrimDocumentation() ?? string.Empty;
            if (ns.MemberType == PythonMemberType.Module) {
                return FormatModuleDocumentation(doc);
            }
            // Documentation can contain something like
            //      func(a, b, c)\n\nThis function...
            // i.e. with paragraph. We want to remove line breaks 
            // in the text after the paragraph breaks and tooltip UI 
            // to decide of the wrap and flow.
            var ctr = 0;
            var seenParagraphGap = false;
            var result = new StringBuilder(doc.Length);
            foreach (var c in doc) {
                if (c == '\r') {
                    continue;
                }
                if (c == '\n') {
                    if (seenParagraphGap) {
                        result.Append(' ');
                    } else {
                        ctr++;
                        if (ctr < 3) {
                            result.AppendLine();
                            seenParagraphGap = ctr == 2;
                        }
                    }
                } else {
                    result.Append(c);
                    ctr = 0;
                }
            }
            return result.ToString().Trim();
        }

        private static string FormatModuleDocumentation(string doc) {
            // Module doc does not nave example/signature lines like a function
            // so just make it flow nicely in the tooltip by removing like breaks.
            // Preserve double breaks and breaks before the indented text
            var lines = doc.Replace("\r", string.Empty).Split('\n');
            var sb = new StringBuilder();
            for (var i = 0; i < lines.Length; i++) {
                var line = lines[i];
                if (i < lines.Length - 1) {
                    if (line.Length == 0) {
                        sb.AppendLine();
                        sb.AppendLine();
                    } else {
                        if (lines[i + 1].Length > 0) {
                            sb.Append(line);
                            sb.Append(' ');
                        } else {
                            sb.Append(line);
                        }
                    }
                } else {
                    sb.Append(line);
                }
            }
            return sb.ToString();
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

        public PythonMemberType MemberType => _type.Value;

        private PythonMemberType GetMemberType() {
            var includesNone = false;
            var result = PythonMemberType.Unknown;

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

        public override bool Equals(object obj) {
            if (!(obj is MemberResult)) {
                return false;
            }
            return Name == ((MemberResult)obj).Name;
        }

        public static bool operator ==(MemberResult x, MemberResult y) => x.Name == y.Name;
        public static bool operator !=(MemberResult x, MemberResult y) => x.Name != y.Name;
        public override int GetHashCode() => Name.GetHashCode();
    }
}

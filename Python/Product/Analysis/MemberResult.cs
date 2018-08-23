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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Analysis.Values;
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
            Scope = null;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            _type = UnknownType;
            _type = new Lazy<PythonMemberType>(GetMemberType);
        }

        public MemberResult(string name, PythonMemberType type) {
            Name = Completion = name;
            Scope = null;
            _type = new Lazy<PythonMemberType>(() => type);
            _vars = EmptyValues;
        }

        public MemberResult(string name, string completion, IEnumerable<AnalysisValue> vars, PythonMemberType? type) :
            this(name, completion, null, vars, type) { }

        internal MemberResult(string name, string completion, InterpreterScope scope, IEnumerable<AnalysisValue> vars, PythonMemberType? type) {
            Name = name;
            _vars = new Lazy<IEnumerable<AnalysisValue>>(() => vars.MaybeEnumerate());
            Completion = completion;
            Scope = scope;
            _type = UnknownType;
            if (type != null) {
                _type = new Lazy<PythonMemberType>(() => type.Value);
            } else {
                _type = new Lazy<PythonMemberType>(GetMemberType);
            }
        }

        internal MemberResult(string name, Func<IEnumerable<AnalysisValue>> vars, Func<PythonMemberType> type) {
            Name = Completion = name;
            Scope = null;
            _vars = vars == null ? EmptyValues : new Lazy<IEnumerable<AnalysisValue>>(vars);
            _type = type == null ? UnknownType : new Lazy<PythonMemberType>(type);
        }
        #endregion

        public MemberResult FilterCompletion(string completion) => new MemberResult(Name, completion, Values, MemberType);

        public string Name { get; }
        public string Completion { get; }
        internal InterpreterScope Scope { get; }

        /// <summary>
        /// Gets the location(s) for the member(s) if they are available.
        /// 
        /// New in 1.5.
        /// </summary>
        public IEnumerable<LocationInfo> Locations => Values.SelectMany(ns => ns.Locations);

        internal IEnumerable<AnalysisValue> Values => _vars.Value;

        public string Documentation
            => DocumentationBuilder.Create(null).GetDocumentation(SeparateMultipleMembers(Values), string.Empty);

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

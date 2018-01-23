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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents an instance of super() bound to a certain specific class and, optionally, to an object instance.
    /// </summary>
    internal class SuperInfo : AnalysisValue {
        private AnalysisUnit _analysisUnit;
        private readonly ClassInfo _classInfo;
        private readonly IAnalysisSet _instances;

        public SuperInfo(ClassInfo classInfo, IAnalysisSet instances = null) {
            _classInfo = classInfo;
            _instances = instances ?? AnalysisSet.Empty;
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return _analysisUnit;
            }
        }

        internal void SetAnalysisUnit(AnalysisUnit unit) {
            Debug.Assert(_analysisUnit == null);
            _analysisUnit = unit;
        }

        public ClassInfo ClassInfo {
            get { return _classInfo; }
        }

        public override string ShortDescription {
            get {
                return "super";
            }
        }

        public override string Description {
            get {
                return "super for " + ClassInfo.Name;
            }
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            var mro = ClassInfo._mro;
            if (!mro.IsValid) {
                return new Dictionary<string, IAnalysisSet>();
            }

            if (options.HasFlag(GetMemberOptions.DeclaredOnly)) {
                return Values.Mro.GetAllMembersOfMro(mro.Skip(1).Take(1), moduleContext, options);
            }

            // First item in MRO list is always the class itself.
            return Values.Mro.GetAllMembersOfMro(mro.Skip(1), moduleContext, options);
        }

        private AnalysisValue GetObjectMember(IModuleContext moduleContext, string name) {
            return AnalysisUnit.State.GetAnalysisValueFromObjects(AnalysisUnit.State.Types[BuiltinTypeId.Object].GetMember(moduleContext, name));
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var mro = ClassInfo._mro;
            if (!mro.IsValid) {
                return AnalysisSet.Empty;
            }

            mro.AddDependency(unit);

            // First item in MRO list is always the class itself.
            var member = Values.Mro.GetMemberFromMroNoReferences(mro.Skip(1), node, unit, name, addRef: true);
            if (member == null) {
                return AnalysisSet.Empty;
            }

            var instances = _instances.Any() ? _instances : unit.State._noneInst.SelfSet;
            IAnalysisSet result = AnalysisSet.Empty;
            foreach (var instance in instances) {
                var desc = member.GetDescriptor(node, instance, this, unit);
                result = result.Union(desc);
            }

            return result;
        }
    }
}

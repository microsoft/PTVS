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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Base class for things which get their members primarily via a built-in .NET type.
    /// </summary>
    class BuiltinNamespace<MemberContainerType> : AnalysisValue where MemberContainerType : IMemberContainer {
        private readonly PythonAnalyzer _projectState;
        internal readonly MemberContainerType _type;
        internal Dictionary<string, IAnalysisSet> _specializedValues;

        public BuiltinNamespace(MemberContainerType pythonType, PythonAnalyzer projectState) {
            _projectState = projectState ?? throw new ArgumentNullException(nameof(projectState)); ;
            _type = pythonType;
            // Ideally we'd assert here whenever pythonType is null, but that
            // makes debug builds unusable because it happens so often.
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            var res = AnalysisSet.Empty;

            IAnalysisSet specializedRes;
            if (_specializedValues != null && _specializedValues.TryGetValue(name, out specializedRes)) {
                return specializedRes;
            }

            if (_type == null) {
                return unit.State.ClassInfos[BuiltinTypeId.NoneType].Instance;
            }

            var member = _type.GetMember(unit.DeclaringModule.InterpreterContext, name);
            if (member != null) {
                res = ProjectState.GetAnalysisValueFromObjects(member);
            }
            return res;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (_type == null) {
                return new Dictionary<string, IAnalysisSet>();
            }
            return ProjectState.GetAllMembers(_type, moduleContext);
        }

        public IAnalysisSet this[string name] {
            get {
                IAnalysisSet value;
                if (TryGetMember(name, out value)) {
                    return value;
                }
                throw new KeyNotFoundException("Key {0} not found".FormatInvariant(name));
            }
            set {
                if (_specializedValues == null) {
                    _specializedValues = new Dictionary<string, IAnalysisSet>();
                }
                _specializedValues[name] = value;
            }
        }

        public bool TryGetMember(string name, out IAnalysisSet value) {
            IAnalysisSet res;
            if (_specializedValues != null && _specializedValues.TryGetValue(name, out res)) {
                value = res;
                return true;
            }
            if (_type == null) {
                value = null;
                return false;
            }
            var member = _type.GetMember(ProjectState._defaultContext, name);
            if (member != null) {
                value = ProjectState.GetAnalysisValueFromObjects(member);
                return true;
            }
            value = null;
            return false;
        }

        public PythonAnalyzer ProjectState {
            get {
                return _projectState;
            }
        }

        public MemberContainerType ContainedValue {
            get {
                return _type;
            }
        }

        public virtual ILocatedMember GetLocatedMember() {
            return null;
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                ILocatedMember locatedMem = GetLocatedMember();
                if (locatedMem != null) {
                    return locatedMem.Locations;
                }
                return LocationInfo.Empty;
            }
        }

        public override bool Equals(object obj) {
            if (obj is BuiltinNamespace<MemberContainerType> bn && GetType() == bn.GetType()) {
                if (_type != null) {
                    return _type.Equals(bn._type);
                }
                return bn._type == null;
            }
            return false;
        }

        public override int GetHashCode() => new { hc1 = GetType().GetHashCode(), hc2 = _type?.GetHashCode() }.GetHashCode();
    }
}

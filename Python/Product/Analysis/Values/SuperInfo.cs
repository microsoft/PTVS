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

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
            var mro = ClassInfo._mro;
            if (!mro.IsValid) {
                return new Dictionary<string, IAnalysisSet>();
            }
            // First item in MRO list is always the class itself.
            return Values.Mro.GetAllMembersOfMro(mro.Skip(1), moduleContext);
        }

        private AnalysisValue GetObjectMember(IModuleContext moduleContext, string name) {
            return AnalysisUnit.ProjectState.GetAnalysisValueFromObjects(AnalysisUnit.ProjectState.Types[BuiltinTypeId.Object].GetMember(moduleContext, name));
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var ignored = base.GetMember(node, unit, name);

            var mro = ClassInfo._mro;
            if (!mro.IsValid) {
                return AnalysisSet.Empty;
            }

            // First item in MRO list is always the class itself.
            var member = Values.Mro.GetMemberFromMroNoReferences(mro.Skip(1), node, unit, name, addRef: true);
            if (member == null) {
                return AnalysisSet.Empty;
            }

            var instances = _instances.Any() ? _instances : unit.ProjectState._noneInst.SelfSet;
            IAnalysisSet result = AnalysisSet.Empty;
            foreach (var instance in instances) {
                var desc = member.GetDescriptor(node, instance, this, unit);
                result = result.Union(desc);
            }

            return result;
        }
    }
}

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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents an instance of super() bound to a certain specific class and, optionally, to an object instance.
    /// </summary>
    internal class SuperInfo : Namespace {
        private AnalysisUnit _analysisUnit;
        private readonly ClassInfo _classInfo;
        private readonly INamespaceSet _instances;

        public SuperInfo(ClassInfo classInfo, INamespaceSet instances = null) {
            _classInfo = classInfo;
            _instances = instances ?? NamespaceSet.Empty;
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

        public override IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            var mro = ClassInfo.Mro;
            if (!mro.IsValid) {
                return new Dictionary<string, INamespaceSet>();
            }
            // First item in MRO list is always the class itself.
            return Mro.GetAllMembersOfMro(mro.Skip(1), moduleContext);
        }

        private Namespace GetObjectMember(IModuleContext moduleContext, string name) {
            return AnalysisUnit.ProjectState.GetNamespaceFromObjects(AnalysisUnit.ProjectState.Types[BuiltinTypeId.Object].GetMember(moduleContext, name));
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            var mro = ClassInfo.Mro;
            if (!mro.IsValid) {
                return NamespaceSet.Empty;
            }

            // First item in MRO list is always the class itself.
            var member = Mro.GetMemberFromMroNoReferences(mro.Skip(1), node, unit, name, addRef: true);
            if (member == null) {
                return NamespaceSet.Empty;
            }

            var instances = _instances.Any() ? _instances : unit.ProjectState._noneInst.SelfSet;
            INamespaceSet result = NamespaceSet.Empty;
            foreach (var instance in instances) {
                var desc = member.GetDescriptor(node, instance, this, unit);
                result = result.Union(desc);
            }

            return result;
        }
    }
}

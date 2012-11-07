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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents an instance of super() bound to a certain specific class and, optionally, to an object instance.
    /// </summary>
    internal class SuperInfo : UserDefinedInfo {
        private readonly ClassInfo _classInfo;
        private readonly ISet<Namespace> _instances;

        public SuperInfo(AnalysisUnit unit, ClassInfo classInfo, ISet<Namespace> instances = null)
            : base(unit) {
            _classInfo = classInfo;
            _instances = instances;
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

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            var mro = ClassInfo.Mro;
            if (!mro.IsValid) {
                return new Dictionary<string, ISet<Namespace>>();
            }
            // First item in MRO list is always the class itself.
            return Mro.GetAllMembersOfMro(mro.Skip(1), moduleContext);
        }

        private Namespace GetObjectMember(IModuleContext moduleContext, string name) {
            return _analysisUnit.ProjectState.GetNamespaceFromObjects(_analysisUnit.ProjectState.Types.Object.GetMember(moduleContext, name));
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            var mro = ClassInfo.Mro;
            if (!mro.IsValid) {
                return EmptySet<Namespace>.Instance;
            }

            // First item in MRO list is always the class itself.
            var member = Mro.GetMemberFromMroNoReferences(mro.Skip(1), node, unit, name, addRef: true);
            if (member == null) {
                return EmptySet<Namespace>.Instance;
            }

            var instances = _instances ?? unit.ProjectState._noneInst.SelfSet;
            bool ownInstances = false;
            ISet<Namespace> result = null;
            foreach (var instance in instances) {
                var desc = member.GetDescriptor(node, instance, this, unit);
                result = result.Union(desc, ref ownInstances);
            }

            return result;
        }
    }
}

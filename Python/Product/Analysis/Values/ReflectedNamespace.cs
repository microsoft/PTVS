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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a .NET namespace as exposed to Python
    /// </summary>
    internal class ReflectedNamespace : BuiltinNamespace<IMemberContainer>, IReferenceableContainer {
        private readonly MemberReferences _references = new MemberReferences();
        private readonly IMemberContainer _container;

        public ReflectedNamespace(IMemberContainer member, PythonAnalyzer projectState)
            : base(member, projectState) {
            _container = member;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _references.AddReference(node, unit, name);
            }
            return res;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
            return ProjectState.GetAllMembers(_container, moduleContext);
        }

        public override PythonMemberType MemberType {
            get {
                if (_container is IMember) {
                    return ((IMember)_container).MemberType;
                }
                return PythonMemberType.Namespace;
            }
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _references.GetDefinitions(name, _container, ProjectState._defaultContext);
        }

        #endregion
    }
}

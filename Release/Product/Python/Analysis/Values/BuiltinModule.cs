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
    internal class BuiltinModule : BuiltinNamespace<IPythonModule>, IReferenceableContainer {
        private readonly MemberReferences _references = new MemberReferences();
        private readonly IPythonModule _interpreterModule;

        public BuiltinModule(IPythonModule module, PythonAnalyzer projectState)
            : base(module, projectState) {
            _interpreterModule = module;
        }

        public IPythonModule InterpreterModule {
            get {
                return _interpreterModule;
            }
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _references.AddReference(node, unit, name);
            }
            return res;
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return ProjectState.GetAllMembers(_interpreterModule, moduleContext);
        }        

        public override string Description {
            get {
                return "built-in module " + _interpreterModule.Name;
            }
        }

        public string Name {
            get {
                return _interpreterModule.Name;
            }
        }

        public override PythonMemberType ResultType {
            get { return _interpreterModule.MemberType; }
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _references.GetDefinitions(name);
        }

        #endregion

        internal IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _type.GetMemberNames(moduleContext);
        }
    }
}

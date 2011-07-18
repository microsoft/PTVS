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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Base class for things which get their members primarily via a built-in .NET type.
    /// </summary>
    class BuiltinNamespace<MemberContainerType> : Namespace where MemberContainerType : IMemberContainer {
        private readonly PythonAnalyzer _projectState;
        internal readonly MemberContainerType _type;
        internal Dictionary<string, ISet<Namespace>> _specializedValues;

        public BuiltinNamespace(MemberContainerType pythonType, PythonAnalyzer projectState) {
            _projectState = projectState;
            _type = pythonType;
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> specialziedRes;
            if (_specializedValues != null && _specializedValues.TryGetValue(name, out specialziedRes)) {
                return specialziedRes;
            }

            var res = _type.GetMember(unit.DeclaringModule.InterpreterContext, name);
            if (res != null) {
                return ProjectState.GetNamespaceFromObjects(res).SelfSet;
            }
            return EmptySet<Namespace>.Instance;
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return ProjectState.GetAllMembers(_type, moduleContext);
        }

        public ISet<Namespace> this[string name] {
            get {
                ISet<Namespace> value;
                if (TryGetMember(name, out value)) {
                    return value;
                }
                throw new KeyNotFoundException(String.Format("Key {0} not found", name));
            }
            set {
                if (_specializedValues == null) {
                    _specializedValues = new Dictionary<string, ISet<Namespace>>();
                }
                _specializedValues[name] = value;
            }
        }

        public bool TryGetMember(string name, out ISet<Namespace> value) {
            ISet<Namespace> res;
            if (_specializedValues != null && _specializedValues.TryGetValue(name, out res)) {
                value = res;
                return true;
            }
            var member = _type.GetMember(ProjectState._defaultContext, name);
            if (member != null) {
                value = ProjectState.GetNamespaceFromObjects(member);
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
    }
}

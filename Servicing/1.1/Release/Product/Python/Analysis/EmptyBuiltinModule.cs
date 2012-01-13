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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    class EmptyBuiltinModule : IBuiltinPythonModule {
        private readonly string _name;

        public EmptyBuiltinModule(string name) {
            _name = name;
        }

        #region IBuiltinPythonModule Members

        public IMember GetAnyMember(string name) {
            return null;
        }

        #endregion

        #region IPythonModule Members

        public string Name {
            get { return _name; }
        }

        public IEnumerable<string> GetChildrenModules() {
            yield break;
        }

        public void Imported(IModuleContext context) {
        }

        #endregion

        #region IMemberContainer Members

        public IMember GetMember(IModuleContext context, string name) {
            return null;
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            yield break;
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Module; }
        }

        #endregion
    }
}

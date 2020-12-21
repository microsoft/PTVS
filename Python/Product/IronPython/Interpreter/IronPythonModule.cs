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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using IronPython.Runtime;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonModule : PythonObject, IPythonModule {
        private string _name;

        public IronPythonModule(IronPythonInterpreter interpreter, ObjectIdentityHandle mod, string name = null)
            : base(interpreter, mod) {
            _name = name;
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Module;
            }
        }

        #region IPythonModule Members

        public string Name {
            get {
                if (_name == null) {
                    var ri = RemoteInterpreter;
                    _name = ri != null ? ri.GetModuleName(Value) : string.Empty;
                }
                return _name;
            }
        }

        public void Imported(IModuleContext context) {
            if (Name == "clr") {
                ((IronPythonModuleContext)context).ShowClr = true;
            } else if (Name == "_wpf") {
                AddWpfReferences();
            }
        }

        public IEnumerable<string> GetChildrenModules() {
            return new string[0];
        }

        private void AddWpfReferences() {
            var ri = RemoteInterpreter;
            if (ri != null && ri.LoadWpf()) {
                Interpreter.RaiseModuleNamesChanged();
            }
        }

        #endregion

        #region IPythonModule2 Members

        public string Documentation {
            get {
                var ri = RemoteInterpreter;
                return ri != null ? ri.GetModuleDocumentation(Value) : string.Empty;
            }
        }

        #endregion
    }
}

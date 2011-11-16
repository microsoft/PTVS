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
using IronPython.Runtime;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonModule : PythonObject, IPythonModule2 {
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
                    _name = Interpreter.Remote.GetModuleName(Value);
                }
                return _name; 
            }
        }

        public void Imported(IModuleContext context) {
            if (Name == "clr") {
                ((IronPythonModuleContext)context).ShowClr = true;
            } else if (Name == "wpf") {
                AddWpfReferences();
            }
        }

        public IEnumerable<string> GetChildrenModules() {
            return new string[0];
        }

        private void AddWpfReferences() {
            if (Interpreter.Remote.LoadWpf()) {
                Interpreter.RaiseModuleNamesChanged();
            }
        }

        #endregion

        #region IPythonModule2 Members

        public string Documentation {
            get {
                return Interpreter.Remote.GetModuleDocumentation(Value);
            }
        }

        #endregion
    }
}

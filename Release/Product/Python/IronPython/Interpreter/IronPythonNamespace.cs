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
using Microsoft.Scripting.Actions;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonNamespace : PythonObject, IPythonModule {
        public IronPythonNamespace(IronPythonInterpreter interpreter, ObjectIdentityHandle ns)
            : base(interpreter, ns) {
        }

        #region IPythonModule Members

        public string Name {
            get { return Interpreter.Remote.GetNamespaceName(Value); }
        }

        public void Imported(IModuleContext context) {
            ((IronPythonModuleContext)context).ShowClr = true;
        }

        public IEnumerable<string> GetChildrenModules() {
            return Interpreter.Remote.GetNamespaceChildren(Value);
        }

        #endregion
    }
}

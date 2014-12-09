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
using Microsoft.PythonTools.InterpreterList;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for opening the interpreter list.
    /// </summary>
    class OpenInterpreterListCommand : Command {
        private readonly IServiceProvider _provider;

        public OpenInterpreterListCommand(IServiceProvider provider) {
            _provider = provider;
        }

        public override void DoCommand(object sender, EventArgs args) {
            _provider.ShowInterpreterList();
        }

        public string Description {
            get {
                return "Python Environments";
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidInterpreterList; }
        }
    }
}

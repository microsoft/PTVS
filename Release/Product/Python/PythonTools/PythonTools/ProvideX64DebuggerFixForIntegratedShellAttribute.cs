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

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools {
#if DEV11_OR_LATER // TODO: UNSURE IF WE NEED THIS FOR DEV12 OR NOT
    /// <summary>
    /// VS 2012 integrated shell doesn't successfully debug x64 apps out of the box due to a bug.  This fixes that bug.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideX64DebuggerFixForIntegratedShellAttribute : RegistrationAttribute {
        public override void Register(RegistrationContext context) {
            using (var engineKey = context.CreateKey("Debugger")) {
                engineKey.SetValue("msvsmon-pseudo_remote", "$ShellFolder$\\Common7\\Packages\\Debugger\\X64\\msvsmon.exe");
            }
        }

        public override void Unregister(RegistrationContext context) {            
        }
    }
#endif
}

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
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Project.Automation;

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid(CommonConstants.ProjectNodePropertiesGuid)]
    public class PythonProjectNodeProperties : CommonProjectNodeProperties {

        public PythonProjectNodeProperties(PythonProjectNode node)
            : base(node) {
        }


        [Browsable(false)]
        public string InterpreterId {
            get {
                return ((PythonProjectNode)this.Node).GetInterpreterFactory().Id.ToString();
            }
        }

        [Browsable(false)]
        public string InterpreterVersion {
            get {
                return ((PythonProjectNode)this.Node).GetInterpreterFactory().Configuration.Version.ToString();
            }
        }

        [PropertyNameAttribute("WebApplication.AspNetDebugging")]
        [Browsable(false)]
        public bool AspNetDebugging {
            get {
                return true;
            }
        }

        [PropertyNameAttribute("WebApplication.NativeDebugging")]
        [Browsable(false)]
        public bool NativeDebugging {
            get {
                return false;
            }
        }

        [Browsable(false)]
        public uint TargetFramework {
            get {
                return 0x40000;
            }
        }
    }
}

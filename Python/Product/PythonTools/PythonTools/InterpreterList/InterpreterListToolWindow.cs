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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.InterpreterList {
    [Guid(PythonConstants.InterpreterListToolWindowGuid)]
    sealed class InterpreterListToolWindow : ToolWindowPane {
        public InterpreterListToolWindow() {
            BitmapResourceID = PythonConstants.ResourceIdForReplImages;
            BitmapIndex = 0;
            Caption = SR.GetString(SR.Environments);
        }

        public override object Content {
            get {
                if (base.Content == null) {
                    base.Content = new InterpreterList(
                        this.GetComponentModel().GetService<IInterpreterOptionsService>(),
                        this
                    );
                }
                return base.Content;
            }
            set {
                base.Content = value;
            }
        }
    }
}

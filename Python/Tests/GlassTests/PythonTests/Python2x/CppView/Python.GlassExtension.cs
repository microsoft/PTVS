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
using System.Reflection;
using Microsoft.VisualStudio.Debugger.Glass;
using Microsoft.PythonTools.Debugger.Concord;

public class SetDebuggerOption : IGlassExtension {
    public void RunTest(ExtensionHost host, string[] args) {
        string name = args[0];
        switch (name) {
            case "ShowNativePythonFrames":
                DebuggerOptions.ShowNativePythonFrames = bool.Parse(args[1]);
                break;
            case "UsePythonStepping":
                DebuggerOptions.UsePythonStepping = bool.Parse(args[1]);
                break;
            case "ShowCppViewNodes":
                DebuggerOptions.ShowCppViewNodes = bool.Parse(args[1]);
                break;
            case "ShowPythonViewNodes":
                DebuggerOptions.ShowPythonViewNodes = bool.Parse(args[1]);
                break;
            default:
                throw new ArgumentException();
        }

    }
}

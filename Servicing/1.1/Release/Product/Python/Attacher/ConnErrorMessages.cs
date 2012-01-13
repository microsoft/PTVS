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


namespace Microsoft.PythonTools.Debugger {
    public enum ConnErrorMessages {
        None,
        InterpreterNotInitialized,
        UnknownVersion,
        LoadDebuggerFailed,
        LoadDebuggerBadDebugger,
        PythonNotFound,
        TimeOut,
        CannotOpenProcess,
        OutOfMemory,
        CannotInjectThread,
        SysNotFound,
        SysSetTraceNotFound,
        SysGetTraceNotFound,
        PyDebugAttachNotFound
    };

    static class ConnErrorExtensions {
        internal static string GetErrorMessage(this ConnErrorMessages attachRes) {
            string msg;
            switch (attachRes) {
                case ConnErrorMessages.CannotInjectThread: msg = "Cannot create thread in debuggee process"; break;
                case ConnErrorMessages.CannotOpenProcess: msg = "Cannot open process for debugging"; break;
                case ConnErrorMessages.InterpreterNotInitialized: msg = "Python interpreter has not been initialized in this process"; break;
                case ConnErrorMessages.LoadDebuggerBadDebugger: msg = "Failed to load debugging script (incorrect version of script?)"; break;
                case ConnErrorMessages.LoadDebuggerFailed: msg = "Failed to compile debugging script"; break;
                case ConnErrorMessages.OutOfMemory: msg = "Out of memory"; break;
                case ConnErrorMessages.PythonNotFound: msg = "Python interpreter not found"; break;
                case ConnErrorMessages.TimeOut: msg = "Timeout while attaching"; break;
                case ConnErrorMessages.UnknownVersion: msg = "Unknown Python version loaded in process"; break;
                case ConnErrorMessages.SysNotFound: msg = "sys module not found"; break;
                case ConnErrorMessages.SysSetTraceNotFound: msg = "settrace not found in sys module"; break;
                case ConnErrorMessages.SysGetTraceNotFound: msg = "gettrace not found in sys module"; break;
                case ConnErrorMessages.PyDebugAttachNotFound: msg = "Cannot find PyDebugAttach.dll at " + attachRes; break;
                default: msg = "Unknown error"; break;
            }
            return msg;
        }
    }
}

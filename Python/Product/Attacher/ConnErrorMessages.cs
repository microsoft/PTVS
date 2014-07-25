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
using System.Collections.Generic;

namespace Microsoft.PythonTools.Debugger {
    // Error messages - must be kept in sync with PyDebugAttach.cpp
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
        PyDebugAttachNotFound,
        RemoteNetworkError,
        RemoteSslError,
        RemoteUnsupportedServer,
        RemoteSecretMismatch,
        RemoteAttachRejected,
        RemoteInvalidUri,
        RemoteUnsupportedTransport,
    };

    static class ConnErrorExtensions {
        private static readonly Dictionary<ConnErrorMessages, string> errMessages = new Dictionary<ConnErrorMessages, string>() {
            { ConnErrorMessages.CannotInjectThread, "Cannot create thread in debuggee process" },
            { ConnErrorMessages.CannotOpenProcess, "Cannot open process for debugging" },
            { ConnErrorMessages.InterpreterNotInitialized, "Python interpreter has not been initialized in this process" },
            { ConnErrorMessages.LoadDebuggerBadDebugger, "Failed to load debugging script (incorrect version of script?)" },
            { ConnErrorMessages.LoadDebuggerFailed, "Failed to compile debugging script" },
            { ConnErrorMessages.OutOfMemory, "Out of memory" },
            { ConnErrorMessages.PythonNotFound, "Python interpreter not found" },
            { ConnErrorMessages.TimeOut, "Timeout while attaching" },
            { ConnErrorMessages.UnknownVersion, "Unknown Python version loaded in process" },
            { ConnErrorMessages.SysNotFound, "sys module not found" },
            { ConnErrorMessages.SysSetTraceNotFound, "settrace not found in sys module" },
            { ConnErrorMessages.SysGetTraceNotFound, "gettrace not found in sys module" },
            { ConnErrorMessages.PyDebugAttachNotFound, "Cannot find PyDebugAttach.dll" },
            { ConnErrorMessages.RemoteNetworkError, "Network error while connecting to remote debugging server" },
            { ConnErrorMessages.RemoteSslError, "Could not establish a secure SSL connection to remote debugging server" },
            { ConnErrorMessages.RemoteUnsupportedServer, "Remote server is not a supported Python Tools for Visual Studio debugging server" },
            { ConnErrorMessages.RemoteSecretMismatch, "Secret specified in the Qualifier string did not match the remote secret" },
            { ConnErrorMessages.RemoteAttachRejected, "Remote debugging server rejected request to attach" },
            { ConnErrorMessages.RemoteInvalidUri, "Invalid remote debugging endpoint URI" },
            { ConnErrorMessages.RemoteUnsupportedTransport, "This remote debugging transport is not supported by this version of Windows." },
        };

        internal static string GetErrorMessage(this ConnErrorMessages attachRes) {
            string msg;
            if (!errMessages.TryGetValue(attachRes, out msg)) {
                msg = "Unknown error";
            }
            return msg;
        }
    }

    [Serializable]
    public sealed class ConnectionException : Exception {
        public ConnectionException(ConnErrorMessages error) {
            Error = error;
        }

        public ConnectionException(ConnErrorMessages error, Exception innerException)
            : base(null, innerException) {
            Error = error;
        }

        public ConnErrorMessages Error {
            get {
                return (ConnErrorMessages)Data[typeof(ConnErrorMessages)];
            }
            private set {
                Data[typeof(ConnErrorMessages)] = value;
            }
        }

        public override string Message {
            get { return Error.GetErrorMessage(); }
        }
    }
}

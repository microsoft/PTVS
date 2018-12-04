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
        UnsupportedVersion,
    };

    static class ConnErrorExtensions {
        private static readonly Dictionary<ConnErrorMessages, string> errMessages = new Dictionary<ConnErrorMessages, string>() {
            { ConnErrorMessages.CannotInjectThread, Strings.ConnErrorMessages_CannotInjectThread },
            { ConnErrorMessages.CannotOpenProcess, Strings.ConnErrorMessages_CannotOpenProcess },
            { ConnErrorMessages.InterpreterNotInitialized, Strings.ConnErrorMessages_InterpreterNotInitialized },
            { ConnErrorMessages.LoadDebuggerBadDebugger, Strings.ConnErrorMessages_LoadDebuggerBadDebugger },
            { ConnErrorMessages.LoadDebuggerFailed, Strings.ConnErrorMessages_LoadDebuggerFailed },
            { ConnErrorMessages.OutOfMemory, Strings.ConnErrorMessages_OutOfMemory },
            { ConnErrorMessages.PythonNotFound, Strings.ConnErrorMessages_PythonNotFound },
            { ConnErrorMessages.TimeOut, Strings.ConnErrorMessages_TimeOut },
            { ConnErrorMessages.UnknownVersion, Strings.ConnErrorMessages_UnknownVersion },
            { ConnErrorMessages.SysNotFound, Strings.ConnErrorMessages_SysNotFound },
            { ConnErrorMessages.SysSetTraceNotFound, Strings.ConnErrorMessages_SysSetTraceNotFound },
            { ConnErrorMessages.SysGetTraceNotFound, Strings.ConnErrorMessages_SysGetTraceNotFound },
            { ConnErrorMessages.PyDebugAttachNotFound, Strings.ConnErrorMessages_PyDebugAttachNotFound },
            { ConnErrorMessages.RemoteNetworkError, Strings.ConnErrorMessages_RemoteNetworkError },
            { ConnErrorMessages.RemoteSslError, Strings.ConnErrorMessages_RemoteSslError },
            { ConnErrorMessages.RemoteUnsupportedServer, Strings.ConnErrorMessages_RemoteUnsupportedServer },
            { ConnErrorMessages.RemoteSecretMismatch, Strings.ConnErrorMessages_RemoteSecretMismatch },
            { ConnErrorMessages.RemoteAttachRejected, Strings.ConnErrorMessages_RemoteAttachRejected },
            { ConnErrorMessages.RemoteInvalidUri, Strings.ConnErrorMessages_RemoteInvalidUri },
            { ConnErrorMessages.RemoteUnsupportedTransport, Strings.ConnErrorMessages_RemoteUnsupportedTransport },
            { ConnErrorMessages.UnsupportedVersion, Strings.ConnErrorMessages_UnsupportedVersion },
        };

        internal static string GetErrorMessage(this ConnErrorMessages attachRes) {
            string msg;
            if (!errMessages.TryGetValue(attachRes, out msg)) {
                msg = Strings.ConnErrorMessages_UnknownError;
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

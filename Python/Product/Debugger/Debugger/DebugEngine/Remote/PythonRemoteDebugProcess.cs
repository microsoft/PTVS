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
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Windows.Forms;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteDebugProcess : IDebugProcess2, IDebugProcessSecurity2 {

        private readonly PythonRemoteDebugPort _port;
        private readonly int _pid;
        private readonly string _exe;
        private readonly string _username;
        private readonly string _version;

        public PythonRemoteDebugProcess(PythonRemoteDebugPort port, int pid, string exe, string username, string version) {
            this._port = port;
            this._pid = (pid == 0) ? 1 : pid; // attach dialog won't show processes with pid==0
            this._username = username;
            this._exe = string.IsNullOrEmpty(exe) ? "<python>" : exe;
            this._version = version;
        }

        public PythonRemoteDebugPort DebugPort {
            get { return _port; }
        }

        public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach) {
            throw new NotImplementedException();
        }

        public int CanDetach() {
            return 0; // S_OK = true, S_FALSE = false
        }

        public int CauseBreak() {
            return 0; // S_OK = true, S_FALSE = false
        }

        public int Detach() {
            throw new NotImplementedException();
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum) {
            ppEnum = new PythonRemoteEnumDebugPrograms(this);
            return 0;
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum) {
            throw new NotImplementedException();
        }

        public int GetAttachedSessionName(out string pbstrSessionName) {
            throw new NotImplementedException();
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS Fields, PROCESS_INFO[] pProcessInfo) {
            // The various string fields should match the strings returned by GetName - keep them in sync when making any changes here.
            var pi = new PROCESS_INFO();
            pi.Fields = Fields;
            pi.bstrFileName = _exe;
            pi.bstrBaseName = BaseName;
            pi.bstrTitle = Title;
            pi.ProcessId.dwProcessId = (uint)_pid;
            pProcessInfo[0] = pi;
            return 0;
        }

        public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName) {
            // The return value should match the corresponding string field returned from GetInfo - keep them in sync when making any changes here.
            switch (gnType) {
                case enum_GETNAME_TYPE.GN_FILENAME:
                    pbstrName = _exe;
                    break;
                case enum_GETNAME_TYPE.GN_BASENAME:
                    pbstrName = BaseName;
                    break;
                case enum_GETNAME_TYPE.GN_NAME:
                case enum_GETNAME_TYPE.GN_TITLE:
                    pbstrName = _version;
                    break;
                default:
                    pbstrName = null;
                    break;
            }
            return 0;
        }

        public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId) {
            var pidStruct = new AD_PROCESS_ID();
            pidStruct.dwProcessId = (uint)_pid;
            pProcessId[0] = pidStruct;
            return 0;
        }

        public int GetPort(out IDebugPort2 ppPort) {
            ppPort = _port;
            return 0;
        }

        public int GetProcessId(out Guid pguidProcessId) {
            pguidProcessId = Guid.Empty;
            return 0;
        }

        public int GetServer(out IDebugCoreServer2 ppServer) {
            throw new NotImplementedException();
        }

        public int Terminate() {
            throw new NotImplementedException();
        }

        public int GetUserName(out string pbstrUserName) {
            pbstrUserName = _username;
            return 0;
        }

        public int QueryCanSafelyAttach() {
            return 0; // S_OK = true, S_FALSE = false
        }

        // AzureExplorerAttachDebuggerCommand looks up remote processes by name, and has to be updated if the format of this property changes.
        private string BaseName {
            get {
                string fileName = CommonUtils.GetFileOrDirectoryName(_exe);
                if (string.IsNullOrEmpty(fileName)) {
                    fileName = _exe;
                }

                // Strip out the secret to avoid showing it in the process list.
                return fileName + " @ " + new UriBuilder(_port.Uri) { UserName = null };
            }
        }

        private string Title {
            get { return _version; }
        }

        public static PythonRemoteDebugProcess Connect(PythonRemoteDebugPort port) {
            PythonRemoteDebugProcess process = null;

            // Connect to the remote debugging server and obtain process information. If any errors occur, display an error dialog, and keep
            // trying for as long as user clicks "Retry".
            while (true) {
                Stream stream = null;
                ConnectionException connEx = null;
                try {
                    // Process information is not sensitive, so ignore any SSL certificate errors, rather than bugging the user with warning dialogs.
                    stream = PythonRemoteProcess.Connect(port.Uri, false);
                } catch (ConnectionException ex) {
                    connEx = ex;
                }

                using (stream) {
                    if (stream != null) {
                        try {
                            stream.Write(PythonRemoteProcess.InfoCommandBytes);
                            int pid = stream.ReadInt32();
                            string exe = stream.ReadString();
                            string username = stream.ReadString();
                            string version = stream.ReadString();
                            process = new PythonRemoteDebugProcess(port, pid, exe, username, version);
                            break;
                        } catch (IOException ex) {
                            connEx = new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
                        }
                    }

                    if (connEx != null) {
                        string errText;
                        switch (connEx.Error) {
                            case ConnErrorMessages.RemoteUnsupportedServer:
                                errText = string.Format("Remote server at {0} is not a Python Tools for Visual Studio remote debugging server, or its version is not supported.", port.Uri);
                                break;
                            case ConnErrorMessages.RemoteSecretMismatch:
                                errText = string.Format("Secret '{0}' did not match the server secret at {1}. Make sure that the secret is specified correctly in the Qualifier textbox, e.g. tcp://secret@localhost.",
                                    port.Uri.UserInfo, new UriBuilder(port.Uri) { UserName = null, Password = null }.Uri);
                                break;
                            case ConnErrorMessages.RemoteSslError:
                                // User has already got a warning dialog and clicked "Cancel" on that, so no further prompts are needed.
                                return null;
                            default:
                                {
                                    // Azure uses HTTP 503 (Service Unavailable) to indicate that websocket connections are not supported. Show a special error message for that.
                                    var wsEx = connEx.InnerException as WebSocketException;
                                    if (wsEx != null) {
                                        var webEx = wsEx.InnerException as WebException;
                                        if (webEx != null) {
                                            var httpResponse = webEx.Response as HttpWebResponse;
                                            if (httpResponse != null && httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable) {
                                                errText = string.Format("Could not connect to remote Python process at {0}. Make sure that web sockets are enabled for the corresponding web site in Azure portal.", port.Uri);
                                                break;
                                            }
                                        }
                                    }

                                    errText = string.Format("Could not connect to remote Python process at {0}. Make sure that the process is running, and has called ptvsd.enable_attach().", port.Uri);
                                    for (var ex = connEx.InnerException; ex != null; ex = ex.InnerException) {
                                        if (ex.InnerException == null) {
                                            errText += "\r\n\r\nAdditional information:\r\n" + ex.Message;
                                        }
                                    }
                                    break;
                                }
                        }

                        DialogResult dlgRes = MessageBox.Show(errText, null, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                        if (dlgRes != DialogResult.Retry) {
                            break;
                        }
                    }
                }
            }

            return process;
        }
    }
}

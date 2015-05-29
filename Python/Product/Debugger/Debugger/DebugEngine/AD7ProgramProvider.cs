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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // This class implments IDebugProgramProvider2. 
    // This registered interface allows the session debug manager (SDM) to obtain information about programs 
    // that have been "published" through the IDebugProgramPublisher2 interface.
    [ComVisible(true)]
    [Guid(Guids.CLSID_ProgramProvider)]
    public class AD7ProgramProvider : IDebugProgramProvider2 {
        public AD7ProgramProvider() {
        }

        #region IDebugProgramProvider2 Members

        // Obtains information about programs running, filtered in a variety of ways.
        int IDebugProgramProvider2.GetProviderProcessData(enum_PROVIDER_FLAGS Flags, IDebugDefaultPort2 port, AD_PROCESS_ID ProcessId, CONST_GUID_ARRAY EngineFilter, PROVIDER_PROCESS_DATA[] processArray) {

            processArray[0] = new PROVIDER_PROCESS_DATA();

            // we handle creation of the remote program provider ourselves.  This is because we always load our program provider locally which keeps
            // attach working when developing Python Tools and running/debugging from within VS and in the experimental hive.  When we are installed
            // we install into the GAC so these types are available to create and then remote debugging works as well.  When we're running in the
            // experimental hive we are not in the GAC so if we're created outside of VS (e.g. in msvsmon on the local machine) then we can't get
            // at our program provider and debug->attach doesn't work.
            if (port != null && port.QueryIsLocal() == VSConstants.S_FALSE) {
                IDebugCoreServer3 server;
                if (ErrorHandler.Succeeded(port.GetServer(out server))) {
                    IDebugCoreServer90 dbgServer = server as IDebugCoreServer90;
                    if (dbgServer != null) {
                        Guid g = typeof(IDebugProgramProvider2).GUID;
                        IntPtr remoteProviderPunk;

                        int hr = dbgServer.CreateManagedInstanceInServer(typeof(AD7ProgramProvider).FullName, typeof(AD7ProgramProvider).Assembly.FullName, 0, ref g, out remoteProviderPunk);
                        try {
                            if (ErrorHandler.Succeeded(hr)) {
                                var remoteProvider = (IDebugProgramProvider2)Marshal.GetObjectForIUnknown(remoteProviderPunk);
                                return remoteProvider.GetProviderProcessData(Flags, null, ProcessId, EngineFilter, processArray);
                            }
                        } finally {
                            if (remoteProviderPunk != IntPtr.Zero) {
                                Marshal.Release(remoteProviderPunk);
                            }
                        }
                    }
                }
            } else if ((Flags & enum_PROVIDER_FLAGS.PFLAG_GET_PROGRAM_NODES) != 0 ) {
                // The debugger is asking the engine to return the program nodes it can debug. We check
                // each process if it has a python##.dll or python##_d.dll loaded and if it does
                // then we report the program as being a Python process.

                if (DebugAttach.IsPythonProcess((int)ProcessId.dwProcessId)) {
                    IDebugProgramNode2 node = new AD7ProgramNode((int)ProcessId.dwProcessId);

                    IntPtr[] programNodes = { Marshal.GetComInterfaceForObject(node, typeof(IDebugProgramNode2)) };

                    IntPtr destinationArray = Marshal.AllocCoTaskMem(IntPtr.Size * programNodes.Length);
                    Marshal.Copy(programNodes, 0, destinationArray, programNodes.Length);

                    processArray[0].Fields = enum_PROVIDER_FIELDS.PFIELD_PROGRAM_NODES;
                    processArray[0].ProgramNodes.Members = destinationArray;
                    processArray[0].ProgramNodes.dwCount = (uint)programNodes.Length;

                    return VSConstants.S_OK;
                }
            }

            return VSConstants.S_FALSE;
        }

        // Gets a program node, given a specific process ID.
        int IDebugProgramProvider2.GetProviderProgramNode(enum_PROVIDER_FLAGS Flags, IDebugDefaultPort2 port, AD_PROCESS_ID ProcessId, ref Guid guidEngine, ulong programId, out IDebugProgramNode2 programNode) {
            // This method is used for Just-In-Time debugging support, which this program provider does not support
            programNode = null;
            return VSConstants.E_NOTIMPL;
        }


        // Establishes a locale for any language-specific resources needed by the DE. This engine only supports Enu.
        int IDebugProgramProvider2.SetLocale(ushort wLangID) {
            return VSConstants.S_OK;
        }

        // Establishes a callback to watch for provider events associated with specific kinds of processes
        int IDebugProgramProvider2.WatchForProviderEvents(enum_PROVIDER_FLAGS Flags, IDebugDefaultPort2 port, AD_PROCESS_ID ProcessId, CONST_GUID_ARRAY EngineFilter, ref Guid guidLaunchingEngine, IDebugPortNotify2 ad7EventCallback) {
            // The sample debug engine is a native debugger, and can therefore always provide a program node
            // in GetProviderProcessData. Non-native debuggers may wish to implement this method as a way
            // of monitoring the process before code for their runtime starts. For example, if implementing a 
            // 'fob script' debug engine, one could attach to a process which might eventually run 'fob script'
            // before this 'fob script' started.
            //
            // To implement this method, an engine would monitor the target process and call AddProgramNode
            // when the target process started running code which was debuggable by the engine. The 
            // enum_PROVIDER_FLAGS.PFLAG_ATTACHED_TO_DEBUGGEE flag indicates if the request is to start
            // or stop watching the process.

            return VSConstants.S_OK;
        }

        #endregion
    }
}

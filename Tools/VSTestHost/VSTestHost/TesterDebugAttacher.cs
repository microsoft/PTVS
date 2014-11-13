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

#if SUPPORT_TESTER

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools.VSTestHost.Internal {
    sealed class TesterDebugAttacher : MarshalByRefObject {
        /// <summary>
        /// Called by the EXECUTION ENGINE to attach this TESTER to the newly
        /// started TESTEE.
        /// </summary>
        /// <returns>True if the attach occurred; otherwise, false.</returns>
        public bool AttachToProcess(int processId, bool mixedMode) {
            using (var evt = TesterDebugAttacherShared.GetNotifyEvent()) {
                if (evt == null) {
                    return false;
                }

                var debug = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger)) as IVsDebugger3;

                var targets = new VsDebugTargetInfo3[1];
                var results = new VsDebugTargetProcessInfo[1];

                targets[0].bstrExe = string.Format("\0{0:X}", processId);
                targets[0].dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_AlreadyRunning;
                targets[0].guidLaunchDebugEngine = Guid.Empty;
                targets[0].dwDebugEngineCount = 1;

                var engine = mixedMode ?
                    VSConstants.DebugEnginesGuids.ManagedAndNative_guid :
                    VSConstants.DebugEnginesGuids.ManagedOnly_guid;

                var pGuids = Marshal.AllocCoTaskMem(Marshal.SizeOf(engine));
                try {
                    Marshal.StructureToPtr(engine, pGuids, false);
                    targets[0].pDebugEngines = pGuids;

                    ErrorHandler.ThrowOnFailure(debug.LaunchDebugTargets3((uint)targets.Length, targets, results));
                } finally {
                    Marshal.FreeCoTaskMem(pGuids);
                }
                evt.Set();
            }
            return true;
        }

        /// <summary>
        /// Called by another TESTER to close the IPC channel so it can use it.
        /// </summary>
        public void Abort() {
            using (var evt = TesterDebugAttacherShared.GetNotifyEvent()) {
                if (evt != null) {
                    evt.Set();
                }
            }
        }
    }

    static class TesterDebugAttacherShared {
        // This name should not change with VS version.
        private const string DebugConnectionChannelName = "VSTestHost_Debug_06420E12_C5A1_4EEF_B604_406E6A139737";
        private const string NotifyEventName = "VSTestHost_Debug_Notify_06420E12_C5A1_4EEF_B604_406E6A139737";


        // The following static functions are designed to be used by the server
        // to simplify handling the IPC channel.


        internal static EventWaitHandle GetNotifyEvent() {
            EventWaitHandle result;
            if (EventWaitHandle.TryOpenExisting(NotifyEventName, EventWaitHandleRights.Modify, out result)) {
                return result;
            }
            return null;
        }

        private static EventWaitHandle CreateNotifyEvent() {
            EventWaitHandle evt = null;
            bool created = false;
            while (evt == null || !created) {
                if (evt != null) {
                    // Set the existing event and allow a short period to close
                    evt.Set();
                    evt.Dispose();
                    Thread.Sleep(10);
                }

                try {
                    evt = new EventWaitHandle(false, EventResetMode.AutoReset, NotifyEventName, out created);
                } catch (WaitHandleCannotBeOpenedException) {
                    created = false;
                }
            }
            return evt;
        }

        /// <summary>
        /// Opens the server channel, including aborting any existing channels
        /// and retrying.
        /// </summary>
        internal static bool OpenChannel(int retries, out IChannel channel, out EventWaitHandle notifyEvent) {
            // If anyone else is listening, abort them so they close the channel
            using (var evt = GetNotifyEvent()) {
                if (evt != null) {
                    evt.Set();
                }
            }

            channel = null;
            notifyEvent = null;

            while (retries-- > 0) {
                try {
                    // We have to open an IPC channel with a single global name,
                    // since our test adapter has no way to know which VS
                    // instance started it. We will only keep the channel open
                    // for a short period, and close it immediately when we stop
                    // debugging. As long as you don't attempt to debug two unit
                    // tests in two different VS instances at the same time,
                    // nobody should get confused.
                    notifyEvent = CreateNotifyEvent();
                    channel = Internal.VSTestHostPackage.RegisterChannel(DebugConnectionChannelName);
                    return true;
                } catch (RemotingException) {
                    // Someone else is already listening, so we need to abort
                    // them and try again.
                    using (var evt = GetNotifyEvent()) {
                        if (evt != null) {
                            evt.Set();
                        }
                    }
                }
            }

            if (channel != null) {
                ChannelServices.UnregisterChannel(channel);
                channel = null;
            }
            if (notifyEvent != null) {
                notifyEvent.Dispose();
                notifyEvent = null;
            }
            return false;
        }

        /// <summary>
        /// Closes a channel opened by <see cref="OpenChannel"/>.
        /// </summary>
        internal static void CloseChannel(IChannel channel) {
            if (channel != null) {
                ChannelServices.UnregisterChannel(channel);
            }
        }

        /// <summary>
        /// Attaches the host VS debugger to the target process. The host VS is
        /// found by connecting to a shared IPC channel that will have been
        /// opened by the host VS when debugging started.
        /// </summary>
        /// <param name="targetProcessId">
        /// ID of the process to attach to.
        /// </param>
        /// <param name="useMixedMode">
        /// True to use mixed native/CLR debugging.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Failed to attach to the remote instance.
        /// </exception>
        internal static void AttachDebugger(int targetProcessId, bool useMixedMode) {
            try {
                for (int retries = 5; retries > 0; --retries) {
                    if (RemoteInstance.AttachToProcess(targetProcessId, useMixedMode)) {
                        return;
                    }
                    Thread.Sleep(100);
                }
            } catch (Exception ex) {
                throw new InvalidOperationException(Resources.FailedToAttach, ex);
            }
            throw new InvalidOperationException(Resources.FailedToAttach);
        }

        /// <summary>
        /// Cancels the pending wait, if any. Does not throw if we are not
        /// currently waiting for an attach.
        /// </summary>
        public static void CancelWait() {
            using (var evt = GetNotifyEvent()) {
                if (evt != null) {
                    evt.Set();
                }
            }
        }

        /// <summary>
        /// Gets the attacher object from the remote process that is currently
        /// listening.
        /// </summary>
        /// <exception cref="RemotingException">
        /// Unable to get the remote object.
        /// </exception>
        private static TesterDebugAttacher RemoteInstance {
            get {
                return (TesterDebugAttacher)RemotingServices.Connect(
                    typeof(TesterDebugAttacher),
                    string.Format("ipc://{0}/debug", DebugConnectionChannelName)
                );
            }
        }

    }
}

#endif

/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// COM message filter class to prevent RPC_E_CALL_REJECTED error while DTE is busy.
    /// The filter is used by COM to handle incoming/outgoing messages while waiting for response from a synchonous call.
    /// </summary>
    /// <seealso cref="http://msdn.microsoft.com/library/en-us/com/html/e12d48c0-5033-47a8-bdcd-e94c49857248.asp"/>
    [ComVisible(true)]
    internal class RetryMessageFilter : IMessageFilter, IDisposable
    {
        private const uint RetryCall = 99;
        private const uint CancelCall = unchecked((uint)-1);   // For COM this must be -1 but IMessageFilter.RetryRejectedCall returns uint.

        private IMessageFilter m_oldFilter;

        /// <summary>
        /// Constructor.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.TC.TestHostAdapters.NativeMethods.CoRegisterMessageFilter(Microsoft.VisualStudio.OLE.Interop.IMessageFilter,Microsoft.VisualStudio.OLE.Interop.IMessageFilter@)")]
        public RetryMessageFilter()
        {
            // Register the filter.
            NativeMethods.CoRegisterMessageFilter(this, out m_oldFilter);
        }

        #region IDisposable implementation
        /// <summary>
        /// FInalizer.
        /// </summary>
        ~RetryMessageFilter()
        {
            Dispose();
        }

        /// <summary>
        /// Implements IDisposable.Dispose.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.TC.TestHostAdapters.NativeMethods.CoRegisterMessageFilter(Microsoft.VisualStudio.OLE.Interop.IMessageFilter,Microsoft.VisualStudio.OLE.Interop.IMessageFilter@)")]
        public void Dispose()
        {
            // Unregister the filter.
            IMessageFilter ourFilter;
            NativeMethods.CoRegisterMessageFilter(m_oldFilter, out ourFilter);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region IMessageFilter members
        /// <summary>
        /// Provides an ability to filter or reject incoming calls (or callbacks) to an object or a process. 
        /// Called by COM prior to each method invocation originating outside the current process. 
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "InComing")]
        public uint HandleInComingCall(uint dwCallType, IntPtr htaskCaller, uint dwTickCount, INTERFACEINFO[] lpInterfaceInfo)
        {
            // Let current process try process the call.
            return (uint)SERVERCALL.SERVERCALL_ISHANDLED;
        }

        /// <summary>
        /// An ability to choose to retry or cancel the outgoing call or switch to the task specified by threadIdCallee.
        /// Called by COM immediately after receiving SERVERCALL_RETRYLATER or SERVERCALL_REJECTED
        /// from the IMessageFilter::HandleIncomingCall method on the callee's IMessageFilter interface.
        /// </summary>
        /// <returns>
        /// -1: The call should be canceled. COM then returns RPC_E_CALL_REJECTED from the original method call. 
        /// 0..99: The call is to be retried immediately. 
        /// 100 and above: COM will wait for this many milliseconds and then retry the call.
        /// </returns>
        public uint RetryRejectedCall(IntPtr htaskCallee, uint dwTickCount, uint dwRejectType)
        {
            if (dwRejectType == (uint)SERVERCALL.SERVERCALL_RETRYLATER)
            {
                // The server called by this process is busy. Ask COM to retry the outgoing call.
                return RetryCall;
            }
            else
            {
                // Ask COM to cancel the call and return RPC_E_CALL_REJECTED from the original method call. 
                return CancelCall;
            }
        }

        /// <summary>
        /// Called by COM when a Windows message appears in a COM application's message queue 
        /// while the application is waiting for a reply to an outgoing remote call. 
        /// </summary>
        /// <returns>
        /// Tell COM whether: to process the message without interrupting the call, 
        /// to continue waiting, or to cancel the operation. 
        /// </returns>
        public uint MessagePending(IntPtr htaskCallee, uint dwTickCount, uint dwPendingType)
        {
            // Continue waiting for the reply, and do not dispatch the message unless it is a task-switching or window-activation message. 
            // A subsequent message will trigger another call to IMessageFilter::MessagePending. 
            return (uint)PENDINGMSG.PENDINGMSG_WAITDEFPROCESS;
        }
        #endregion
    }
}

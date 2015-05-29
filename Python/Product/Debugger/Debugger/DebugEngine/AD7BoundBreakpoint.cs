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

using System.Diagnostics;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // This class represents a breakpoint that has been bound to a location in the debuggee. It is a child of the pending breakpoint
    // that creates it. Unless the pending breakpoint only has one bound breakpoint, each bound breakpoint is displayed as a child of the
    // pending breakpoint in the breakpoints window. Otherwise, only one is displayed.
    class AD7BoundBreakpoint : IDebugBoundBreakpoint2 {
        private readonly AD7PendingBreakpoint _pendingBreakpoint;
        private readonly AD7BreakpointResolution _breakpointResolution;
        private readonly AD7Engine _engine;
        private readonly PythonBreakpoint _breakpoint;

        private bool _enabled;
        private bool _deleted;

        public AD7BoundBreakpoint(AD7Engine engine, PythonBreakpoint address, AD7PendingBreakpoint pendingBreakpoint, AD7BreakpointResolution breakpointResolution, bool enabled) {
            _engine = engine;
            _breakpoint = address;
            _pendingBreakpoint = pendingBreakpoint;
            _breakpointResolution = breakpointResolution;
            _enabled = enabled;
            _deleted = false;
        }

        #region IDebugBoundBreakpoint2 Members

        // Called when the breakpoint is being deleted by the user.
        int IDebugBoundBreakpoint2.Delete() {
            AssertMainThread();

            if (!_deleted) {
                _deleted = true;
                _breakpoint.Remove();
                _pendingBreakpoint.OnBoundBreakpointDeleted(this);
                _engine.BreakpointManager.RemoveBoundBreakpoint(_breakpoint);
            }

            return VSConstants.S_OK;
        }

        [Conditional("DEBUG")]
        private static void AssertMainThread() {
            //Debug.Assert(Worker.MainThreadId == Worker.CurrentThreadId);
        }

        // Called by the debugger UI when the user is enabling or disabling a breakpoint.
        int IDebugBoundBreakpoint2.Enable(int fEnable) {
            AssertMainThread();

            bool enabled = fEnable == 0 ? false : true;
            if (_enabled != enabled) {
                if (!enabled) {
                    _breakpoint.Disable();
                } else {
                    _breakpoint.Add();
                }
            }
            _enabled = enabled;
            return VSConstants.S_OK;
        }

        // Return the breakpoint resolution which describes how the breakpoint bound in the debuggee.
        int IDebugBoundBreakpoint2.GetBreakpointResolution(out IDebugBreakpointResolution2 ppBPResolution) {
            ppBPResolution = _breakpointResolution;
            return VSConstants.S_OK;
        }

        // Return the pending breakpoint for this bound breakpoint.
        int IDebugBoundBreakpoint2.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBreakpoint) {
            ppPendingBreakpoint = _pendingBreakpoint;
            return VSConstants.S_OK;
        }

        // 
        int IDebugBoundBreakpoint2.GetState(enum_BP_STATE[] pState) {
            pState[0] = 0;

            if (_deleted) {
                pState[0] = enum_BP_STATE.BPS_DELETED;
            } else if (_enabled) {
                pState[0] = enum_BP_STATE.BPS_ENABLED;
            } else if (!_enabled) {
                pState[0] = enum_BP_STATE.BPS_DISABLED;
            }

            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.SetCondition(BP_CONDITION bpCondition) {
            _breakpoint.SetCondition(bpCondition.styleCondition.ToPython(), bpCondition.bstrCondition);
            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.GetHitCount(out uint pdwHitCount) {
            var remoteProcess = _engine.Process as PythonRemoteProcess;
            if (remoteProcess != null && remoteProcess.TargetHostType == AD7Engine.TargetUwp) {
                // Target is UWP host and we will just assume breakpoint hit count is 1 from this
                // remote debug type due to issues with communicating this command
                pdwHitCount = 1;
            } else {
                var task = _breakpoint.GetHitCountAsync();
                pdwHitCount = (uint)task.GetAwaiter().GetResult();
            }

            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.SetHitCount(uint dwHitCount) {
            _breakpoint.SetHitCount((int)dwHitCount);
            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.SetPassCount(BP_PASSCOUNT bpPassCount) {
            _breakpoint.SetPassCount(bpPassCount.stylePassCount.ToPython(), (int)bpPassCount.dwPassCount);
            return VSConstants.S_OK;
        }

        #endregion
    }
}

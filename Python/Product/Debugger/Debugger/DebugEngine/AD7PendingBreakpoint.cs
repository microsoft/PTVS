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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // This class represents a pending breakpoint which is an abstract representation of a breakpoint before it is bound.
    // When a user creates a new breakpoint, the pending breakpoint is created and is later bound. The bound breakpoints
    // become children of the pending breakpoint.
    class AD7PendingBreakpoint : IDebugPendingBreakpoint2 {
        // The breakpoint request that resulted in this pending breakpoint being created.
        private readonly IDebugBreakpointRequest2 _bpRequest;
        private BP_REQUEST_INFO _bpRequestInfo;
        private readonly AD7Engine _engine;
        private readonly BreakpointManager _bpManager;

        private readonly List<AD7BoundBreakpoint> _boundBreakpoints;

        private bool _enabled;
        private readonly bool _deleted;

        public AD7PendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, AD7Engine engine, BreakpointManager bpManager) {
            _bpRequest = pBPRequest;
            BP_REQUEST_INFO[] requestInfo = new BP_REQUEST_INFO[1];
            EngineUtils.CheckOk(_bpRequest.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION | enum_BPREQI_FIELDS.BPREQI_CONDITION | enum_BPREQI_FIELDS.BPREQI_ALLFIELDS, requestInfo));
            _bpRequestInfo = requestInfo[0];

            _engine = engine;
            _bpManager = bpManager;
            _boundBreakpoints = new System.Collections.Generic.List<AD7BoundBreakpoint>();

            _enabled = true;
            _deleted = false;
        }

        private bool CanBind() {
            // The Python engine only supports breakpoints on a file and line number. No other types of breakpoints are supported.
            if (_deleted || _bpRequestInfo.bpLocation.bpLocationType != (uint)enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE) {
                return false;
            }

            return true;
        }

        // Get the document context for this pending breakpoint. A document context is a abstract representation of a source file 
        // location.
        public AD7DocumentContext GetDocumentContext(PythonBreakpoint address) {
            IDebugDocumentPosition2 docPosition = (IDebugDocumentPosition2)(Marshal.GetObjectForIUnknown(_bpRequestInfo.bpLocation.unionmember2));
            string documentName;
            EngineUtils.CheckOk(docPosition.GetFileName(out documentName));

            // Get the location in the document that the breakpoint is in.
            TEXT_POSITION[] startPosition = new TEXT_POSITION[1];
            TEXT_POSITION[] endPosition = new TEXT_POSITION[1];
            EngineUtils.CheckOk(docPosition.GetRange(startPosition, endPosition));

            AD7MemoryAddress codeContext = new AD7MemoryAddress(_engine, documentName, startPosition[0].dwLine);

            return new AD7DocumentContext(documentName, startPosition[0], startPosition[0], codeContext, FrameKind.Python);
        }

        // Remove all of the bound breakpoints for this pending breakpoint
        public void ClearBoundBreakpoints() {
            lock (_boundBreakpoints) {
                for (int i = _boundBreakpoints.Count - 1; i >= 0; i--) {
                    ((IDebugBoundBreakpoint2)_boundBreakpoints[i]).Delete();
                }
            }
        }

        // Called by bound breakpoints when they are being deleted.
        public void OnBoundBreakpointDeleted(AD7BoundBreakpoint boundBreakpoint) {
            lock (_boundBreakpoints) {
                _boundBreakpoints.Remove(boundBreakpoint);
            }
        }

        #region IDebugPendingBreakpoint2 Members

        // Binds this pending breakpoint to one or more code locations.
        int IDebugPendingBreakpoint2.Bind() {
            if (CanBind()) {
                IDebugDocumentPosition2 docPosition = (IDebugDocumentPosition2)(Marshal.GetObjectForIUnknown(_bpRequestInfo.bpLocation.unionmember2));

                // Get the name of the document that the breakpoint was put in
                string documentName;
                EngineUtils.CheckOk(docPosition.GetFileName(out documentName));


                // Get the location in the document that the breakpoint is in.
                TEXT_POSITION[] startPosition = new TEXT_POSITION[1];
                TEXT_POSITION[] endPosition = new TEXT_POSITION[1];
                EngineUtils.CheckOk(docPosition.GetRange(startPosition, endPosition));

                lock (_boundBreakpoints) {
                    if (_bpRequestInfo.guidLanguage == DebuggerConstants.guidLanguagePython) {
                        var bp = _engine.Process.AddBreakpoint(
                            documentName,
                            (int)(startPosition[0].dwLine + 1),
                            _bpRequestInfo.bpCondition.styleCondition.ToPython(),
                            _bpRequestInfo.bpCondition.bstrCondition,
                            _bpRequestInfo.bpPassCount.stylePassCount.ToPython(),
                            (int)_bpRequestInfo.bpPassCount.dwPassCount);

                        AD7BreakpointResolution breakpointResolution = new AD7BreakpointResolution(_engine, bp, GetDocumentContext(bp));
                        AD7BoundBreakpoint boundBreakpoint = new AD7BoundBreakpoint(_engine, bp, this, breakpointResolution, _enabled);
                        _boundBreakpoints.Add(boundBreakpoint);
                        _bpManager.AddBoundBreakpoint(bp, boundBreakpoint);

                        if (_enabled) {
                            TaskHelpers.RunSynchronouslyOnUIThread(ct => bp.AddAsync(ct));
                        }

                        return VSConstants.S_OK;
                    } else if (_bpRequestInfo.guidLanguage == DebuggerConstants.guidLanguageDjangoTemplate) {

                        // bind a Django template 
                        var bp = _engine.Process.AddDjangoBreakpoint(
                            documentName,
                            (int)(startPosition[0].dwLine + 1)
                        );

                        AD7BreakpointResolution breakpointResolution = new AD7BreakpointResolution(_engine, bp, GetDocumentContext(bp));
                        AD7BoundBreakpoint boundBreakpoint = new AD7BoundBreakpoint(_engine, bp, this, breakpointResolution, _enabled);
                        _boundBreakpoints.Add(boundBreakpoint);
                        _bpManager.AddBoundBreakpoint(bp, boundBreakpoint);

                        if (_enabled) {
                            TaskHelpers.RunSynchronouslyOnUIThread(ct => bp.AddAsync(ct));
                        }

                        return VSConstants.S_OK;
                    }
                }
            }

            // The breakpoint could not be bound. This may occur for many reasons such as an invalid location, an invalid expression, etc...
            // The Python engine does not support this.
            // TODO: send an instance of IDebugBreakpointErrorEvent2 to the UI and return a valid instance of IDebugErrorBreakpoint2 from
            // IDebugPendingBreakpoint2::EnumErrorBreakpoints. The debugger will then display information about why the breakpoint did not
            // bind to the user.
            return VSConstants.S_FALSE;
        }

        // Determines whether this pending breakpoint can bind to a code location.
        int IDebugPendingBreakpoint2.CanBind(out IEnumDebugErrorBreakpoints2 ppErrorEnum) {
            ppErrorEnum = null;

            if (!CanBind()) {
                // Called to determine if a pending breakpoint can be bound. 
                // The breakpoint may not be bound for many reasons such as an invalid location, an invalid expression, etc...
                // TODO: return a valid enumeration of IDebugErrorBreakpoint2. The debugger will then display information about why
                // the breakpoint did not bind to the user.
                ppErrorEnum = null;
                return VSConstants.S_FALSE;
            }

            return VSConstants.S_OK;
        }

        // Deletes this pending breakpoint and all breakpoints bound from it.
        int IDebugPendingBreakpoint2.Delete() {
            lock (_boundBreakpoints) {
                for (int i = _boundBreakpoints.Count - 1; i >= 0; i--) {
                    ((IDebugBoundBreakpoint2)_boundBreakpoints[i]).Delete();
                }
            }

            return VSConstants.S_OK;
        }

        // Toggles the enabled state of this pending breakpoint.
        int IDebugPendingBreakpoint2.Enable(int fEnable) {
            lock (_boundBreakpoints) {
                _enabled = fEnable == 0 ? false : true;

                foreach (AD7BoundBreakpoint bp in _boundBreakpoints) {
                    ((IDebugBoundBreakpoint2)bp).Enable(fEnable);
                }
            }

            return VSConstants.S_OK;
        }

        // Enumerates all breakpoints bound from this pending breakpoint
        int IDebugPendingBreakpoint2.EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum) {
            lock (_boundBreakpoints) {
                IDebugBoundBreakpoint2[] boundBreakpoints = _boundBreakpoints.ToArray();
                ppEnum = new AD7BoundBreakpointsEnum(boundBreakpoints);
            }
            return VSConstants.S_OK;
        }

        // Enumerates all error breakpoints that resulted from this pending breakpoint.
        int IDebugPendingBreakpoint2.EnumErrorBreakpoints(enum_BP_ERROR_TYPE bpErrorType, out IEnumDebugErrorBreakpoints2 ppEnum) {
            // Called when a pending breakpoint could not be bound. This may occur for many reasons such as an invalid location, an invalid expression, etc...
            // TODO: send an instance of IDebugBreakpointErrorEvent2 to the UI and return a valid enumeration of IDebugErrorBreakpoint2
            // from IDebugPendingBreakpoint2::EnumErrorBreakpoints. The debugger will then display information about why the breakpoint
            // did not bind to the user.
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        // Gets the breakpoint request that was used to create this pending breakpoint
        int IDebugPendingBreakpoint2.GetBreakpointRequest(out IDebugBreakpointRequest2 ppBPRequest) {
            ppBPRequest = _bpRequest;
            return VSConstants.S_OK;
        }

        // Gets the state of this pending breakpoint.
        int IDebugPendingBreakpoint2.GetState(PENDING_BP_STATE_INFO[] pState) {
            if (_deleted) {
                pState[0].state = (enum_PENDING_BP_STATE)enum_BP_STATE.BPS_DELETED;
            } else if (_enabled) {
                pState[0].state = (enum_PENDING_BP_STATE)enum_BP_STATE.BPS_ENABLED;
            } else {
                pState[0].state = (enum_PENDING_BP_STATE)enum_BP_STATE.BPS_DISABLED;
            }

            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.SetCondition(BP_CONDITION bpCondition) {
            _bpRequestInfo.bpCondition = bpCondition;
            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.SetPassCount(BP_PASSCOUNT bpPassCount) {
            _bpRequestInfo.bpPassCount = bpPassCount;
            return VSConstants.S_OK;
        }

        // Toggles the virtualized state of this pending breakpoint. When a pending breakpoint is virtualized, 
        // the debug engine will attempt to bind it every time new code loads into the program.
        // The Python engine does not support this.
        int IDebugPendingBreakpoint2.Virtualize(int fVirtualize) {
            return VSConstants.S_OK;
        }

        #endregion
    }

    static class BreakpointEnumExtensions {
        public static PythonBreakpointConditionKind ToPython(this enum_BP_COND_STYLE style) {
            switch (style) {
                case enum_BP_COND_STYLE.BP_COND_NONE:
                    return PythonBreakpointConditionKind.Always;
                case enum_BP_COND_STYLE.BP_COND_WHEN_CHANGED:
                    return PythonBreakpointConditionKind.WhenChanged;
                case enum_BP_COND_STYLE.BP_COND_WHEN_TRUE:
                    return PythonBreakpointConditionKind.WhenTrue;
                default:
                    throw new ArgumentException(Strings.UnrecognizedEnumValue.FormatUI(typeof(enum_BP_COND_STYLE)), nameof(style));
            }
        }

        public static PythonBreakpointPassCountKind ToPython(this enum_BP_PASSCOUNT_STYLE style) {
            switch (style) {
                case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE:
                    return PythonBreakpointPassCountKind.Always;
                case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD:
                    return PythonBreakpointPassCountKind.Every;
                case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL:
                    return PythonBreakpointPassCountKind.WhenEqual;
                case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER:
                    return PythonBreakpointPassCountKind.WhenEqualOrGreater;
                default:
                    throw new ArgumentException(Strings.UnrecognizedEnumValue.FormatUI(typeof(enum_BP_PASSCOUNT_STYLE)), nameof(style));
            }
        }
    }
}

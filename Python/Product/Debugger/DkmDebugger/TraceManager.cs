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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Stepping;

namespace Microsoft.PythonTools.DkmDebugger {
    internal unsafe class TraceManager : DkmDataItem {
        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct DebuggerString {
            public const int SizeOf = 4;
            public int length;
            public fixed char data[1];
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct BreakpointData {
            public int maxLineNumber;
            public ulong lineNumbers;
            public ulong fileNamesOffsets;
            public ulong strings;
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct CurrentSourceLocation {
            public int lineNumber;
            public ulong fileName;
        }

        private readonly DkmProcess _process;
        private readonly PythonRuntimeInfo _pyrtInfo;
        private readonly Dictionary<SourceLocation, List<DkmRuntimeBreakpoint>> _breakpoints = new Dictionary<SourceLocation, List<DkmRuntimeBreakpoint>>();

        private readonly ArrayProxy<CliStructProxy<BreakpointData>> _breakpointData;
        private readonly ByteProxy _currentBreakpointData, _breakpointDataInUseByTraceFunc;
        private readonly CliStructProxy<CurrentSourceLocation> _currentSourceLocation;
        private readonly Int32Proxy _stepKind, _steppingStackDepth;
        private readonly UInt64Proxy _stepThreadId;

        private readonly DkmRuntimeBreakpoint _onBreakpointHitBP, _onStepCompleteBP, _onStepFallThroughBP;
        private DkmStepper _stepper;

        public TraceManager(DkmProcess process) {
            _process = process;
            _pyrtInfo = process.GetPythonRuntimeInfo();

            _breakpointData = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<ArrayProxy<CliStructProxy<BreakpointData>>>("breakpointData");
            _currentBreakpointData = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<ByteProxy>("currentBreakpointData");
            _breakpointDataInUseByTraceFunc = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<ByteProxy>("breakpointDataInUseByTraceFunc");
            _currentSourceLocation = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<CurrentSourceLocation>>("currentSourceLocation");
            _stepKind = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<Int32Proxy>("stepKind");
            _stepThreadId = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<UInt64Proxy>("stepThreadId");
            _steppingStackDepth = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<Int32Proxy>("steppingStackDepth");

            var onBreakpointHit = _pyrtInfo.DLLs.DebuggerHelper.FindExportName("OnBreakpointHit", true);
            _onBreakpointHitBP = DkmRuntimeInstructionBreakpoint.Create(Guids.PythonTraceManagerSourceGuid, null, onBreakpointHit, false, null);
            _onBreakpointHitBP.Enable();

            var onStepComplete = _pyrtInfo.DLLs.DebuggerHelper.FindExportName("OnStepComplete", true);
            _onStepCompleteBP = DkmRuntimeInstructionBreakpoint.Create(Guids.PythonTraceManagerSourceGuid, null, onStepComplete, false, null);
            _onStepCompleteBP.Enable();

            var onStepFallThrough = _pyrtInfo.DLLs.DebuggerHelper.FindExportName("OnStepFallThrough", true);
            _onStepFallThroughBP = DkmRuntimeInstructionBreakpoint.Create(Guids.PythonTraceManagerSourceGuid, null, onStepFallThrough, false, null);
            _onStepFallThroughBP.Enable();

            WriteBreakpoints();
        }

        public void OnNativeBreakpointHit(DkmRuntimeBreakpoint nativeBP, DkmThread thread) {
            if (nativeBP == _onBreakpointHitBP) {
                OnBreakpointHit(thread);
            } else if (nativeBP == _onStepCompleteBP) {
                OnStepComplete(thread);
            } else if (nativeBP == _onStepFallThroughBP) {
                OnStepFallThrough(thread);
            } else if (nativeBP.SourceId == Guids.PythonStepTargetSourceGuid) {
                OnStepTargetBreakpoint(thread);
            } else {
                Debug.Fail("BreakpointManager notified about a native breakpoint that it didn't create.");
            }
        }

        public void AddBreakpoint(DkmRuntimeBreakpoint bp) {
            var loc = bp.GetDataItem<SourceLocation>();
            List<DkmRuntimeBreakpoint> bpsAtLoc;
            if (!_breakpoints.TryGetValue(loc, out bpsAtLoc)) {
                _breakpoints[loc] = bpsAtLoc = new List<DkmRuntimeBreakpoint>();
            }
            bpsAtLoc.Add(bp);
            WriteBreakpoints();
        }

        public void RemoveBreakpoint(DkmRuntimeBreakpoint bp) {
            var loc = bp.GetDataItem<SourceLocation>();
            List<DkmRuntimeBreakpoint> bpsAtLoc;
            if (!_breakpoints.TryGetValue(loc, out bpsAtLoc)) {
                return;
            }
            if (!bpsAtLoc.Remove(bp)) {
                return;
            }
            if (bpsAtLoc.Count == 0) {
                _breakpoints.Remove(loc);
            }
            WriteBreakpoints();
        }

        private class StructuralArrayEqualityComparer<T> : EqualityComparer<T[]> {
            public override bool Equals(T[] x, T[] y) {
                return StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
            }

            public override int GetHashCode(T[] obj) {
                return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);
            }
        }

        private void WriteBreakpoints() {
            int maxLineNumber = _breakpoints.Keys.Select(loc => loc.LineNumber).DefaultIfEmpty().Max();
            var lineNumbersStream = new MemoryStream((maxLineNumber + 1) * sizeof(int));
            var lineNumbersWriter = new BinaryWriter(lineNumbersStream);

            var stringsStream = new MemoryStream();
            var stringsWriter = new BinaryWriter(stringsStream);
            var stringOffsets = new Dictionary<string, int>();
            stringsWriter.Write((int)0);
            foreach (var s in _breakpoints.Keys.Select(loc => loc.FileName).Distinct()) {
                stringOffsets[s] = (int)stringsStream.Position;
                stringsWriter.Write((int)s.Length);
                foreach (char c in s) {
                    stringsWriter.Write((ushort)c);
                }
                stringsWriter.Write((ushort)0);
            }

            var fileNamesOffsetsStream = new MemoryStream();
            var fileNamesOffsetsWriter = new BinaryWriter(fileNamesOffsetsStream);
            var fileNamesOffsetsIndices = new Dictionary<int[], int>(new StructuralArrayEqualityComparer<int>());
            fileNamesOffsetsWriter.Write((int)0);
            foreach (var g in _breakpoints.Keys.GroupBy(loc => loc.LineNumber)) {
                var lineNumber = g.Key;

                var fileNamesOffsets = g.Select(loc => stringOffsets[loc.FileName]).ToArray();
                Array.Sort(fileNamesOffsets);

                int fileNamesOffsetsIndex;
                if (!fileNamesOffsetsIndices.TryGetValue(fileNamesOffsets, out fileNamesOffsetsIndex)) {
                    fileNamesOffsetsIndex = (int)fileNamesOffsetsStream.Position / sizeof(int);
                    foreach (int offset in fileNamesOffsets) {
                        fileNamesOffsetsWriter.Write(offset);
                    }
                    fileNamesOffsetsWriter.Write((int)0);
                    fileNamesOffsetsIndices.Add(fileNamesOffsets, fileNamesOffsetsIndex);
                }

                lineNumbersStream.Position = lineNumber * sizeof(int);
                lineNumbersWriter.Write(fileNamesOffsetsIndex);
            }

            byte breakpointDataInUseByTraceFunc = _breakpointDataInUseByTraceFunc.Read();
            byte currentBreakpointData = (breakpointDataInUseByTraceFunc == 0) ? (byte)1 : (byte)0;
            _currentBreakpointData.Write(currentBreakpointData);

            var bpDataProxy = _breakpointData[currentBreakpointData];
            BreakpointData bpData = bpDataProxy.Read();
            if (bpData.lineNumbers != 0) {
                _process.FreeVirtualMemory(bpData.lineNumbers, 0, NativeMethods.MEM_RELEASE);
            }
            if (bpData.fileNamesOffsets != 0) {
                _process.FreeVirtualMemory(bpData.fileNamesOffsets, 0, NativeMethods.MEM_RELEASE);
            }
            if (bpData.strings != 0) {
                _process.FreeVirtualMemory(bpData.strings, 0, NativeMethods.MEM_RELEASE);
            }

            bpData.maxLineNumber = maxLineNumber;
            if (lineNumbersStream.Length > 0) {
                bpData.lineNumbers = _process.AllocateVirtualMemory(0, (int)lineNumbersStream.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                _process.WriteMemory(bpData.lineNumbers, lineNumbersStream.ToArray());
            } else {
                bpData.lineNumbers = 0;
            }

            bpData.fileNamesOffsets = _process.AllocateVirtualMemory(0, (int)fileNamesOffsetsStream.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
            _process.WriteMemory(bpData.fileNamesOffsets, fileNamesOffsetsStream.ToArray());

            bpData.strings = _process.AllocateVirtualMemory(0, (int)stringsStream.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
            _process.WriteMemory(bpData.strings, stringsStream.ToArray());

            bpDataProxy.Write(bpData);
        }

        private void OnBreakpointHit(DkmThread thread) {
            CurrentSourceLocation cbp = _currentSourceLocation.Read();

            DebuggerString fileNameDS;
            _process.ReadMemory(cbp.fileName, DkmReadMemoryFlags.None, &fileNameDS, sizeof(DebuggerString));

            char* fileNameBuf = stackalloc char[fileNameDS.length];
            ulong dataOffset = (ulong)((byte*)fileNameDS.data - (byte*)&fileNameDS);
            _process.ReadMemory(cbp.fileName + dataOffset, DkmReadMemoryFlags.None, fileNameBuf, fileNameDS.length * 2);
            string fileName = new string(fileNameBuf, 0, fileNameDS.length);

            var loc = new SourceLocation(fileName, cbp.lineNumber);
            List<DkmRuntimeBreakpoint> bps;
            if (!_breakpoints.TryGetValue(loc, out bps)) {
                Debug.Fail("TraceFunc signalled a breakpoint at a location that BreakpointManager does not know about.");
                return;
            }

            foreach (var bp in bps) {
                bp.OnHit(thread, false);
            }
        }

        private class StepBeginState : DkmDataItem {
            public ulong FrameBase { get; set; }
        }

        public void BeforeEnableNewStepper(DkmRuntimeInstance runtimeInstance, DkmStepper stepper) {
            ulong retAddr, frameBase, vframe;
            stepper.Thread.GetCurrentFrameInfo(out retAddr, out frameBase, out vframe);
            stepper.SetDataItem(DkmDataCreationDisposition.CreateAlways, new StepBeginState { FrameBase = frameBase });
        }

        public void Step(DkmStepper stepper, DkmStepArbitrationReason reason) {
            var thread = stepper.Thread;
            var process = thread.Process;

            if (stepper.StepKind == DkmStepKind.StepIntoSpecific) {
                throw new NotSupportedException();
            } else if (_stepper != null) {
                _stepper.CancelStepper(process.GetPythonRuntimeInstance());
                _stepper = null;
            }

            // Check if this was a step out (or step over/in that fell through) from native to Python.
            // If so, we consider the step done, since we can report the correct callstack at this point.
            if (reason == DkmStepArbitrationReason.TransitionModule) {
                var beginState = stepper.GetDataItem<StepBeginState>();
                if (beginState != null) {
                    ulong retAddr, frameBase, vframe;
                    thread.GetCurrentFrameInfo(out retAddr, out frameBase, out vframe);
                    if (frameBase >= beginState.FrameBase) {
                        stepper.OnStepComplete(thread, false);
                        return;
                    }
                }
            }

            if (stepper.StepKind == DkmStepKind.Into) {
                new LocalComponent.BeginStepInNotification {
                    ThreadId = thread.UniqueId
                }.SendHigher(process);
            }

            _stepper = stepper;
            _stepKind.Write((int)stepper.StepKind + 1);
            _stepThreadId.Write((uint)thread.SystemPart.Id);
            _steppingStackDepth.Write(0);
        }

        public void CancelStep(DkmStepper stepper) {
            if (_stepper == null) {
                return;
            } else if (stepper != _stepper) {
                Debug.Fail("Trying to cancel a step while no step or another step is in progress.");
                throw new InvalidOperationException();
            } 

            StepDone(stepper.Thread);
        }

        private void OnStepComplete(DkmThread thread) {
            if (_stepper != null) {
                StepDone(thread).OnStepComplete(thread, false);
            }
        }

        private void OnStepTargetBreakpoint(DkmThread thread) {
            if (_stepper == null) {
                Debug.Fail("OnStepTargetBreakpoint called but no step operation is in progress.");
                throw new InvalidOperationException();
            }

            if (_stepper.StepKind == DkmStepKind.Into) {
                StepDone(thread).OnStepArbitration(DkmStepArbitrationReason.ExitRuntime, _process.GetPythonRuntimeInstance());
            } else {
                // Just because we hit the return breakpoint doesn't mean that we've actually returned - it could be 
                // a recursive call. Check stack depth to distinguish this from an actual return.
                var beginState = _stepper.GetDataItem<StepBeginState>();
                if (beginState != null) {
                    ulong retAddr, frameBase, vframe;
                    thread.GetCurrentFrameInfo(out retAddr, out frameBase, out vframe);
                    if (frameBase > beginState.FrameBase) {
                        OnStepComplete(thread);
                    }
                }
            }
        }

        private DkmStepper StepDone(DkmThread thread) {
            new LocalComponent.StepCompleteNotification().SendHigher(thread.Process);
            new LocalStackWalkingComponent.StepCompleteNotification().SendHigher(thread.Process);

            var stepper = _stepper;
            _stepper = null;
            _stepKind.Write(0);
            return stepper;

        }

        private void OnStepFallThrough(DkmThread thread) {
            // Step fell through the end of the frame in which it began - time to register the breakpoint for the return address.
            new LocalStackWalkingComponent.BeginStepOutNotification {
                ThreadId = thread.UniqueId
            }.SendHigher(_process);
        }
    }
}

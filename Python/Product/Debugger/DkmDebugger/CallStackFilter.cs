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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class CallStackFilter : DkmDataItem {
        private class StackWalkContextData : DkmDataItem {
            public bool? IsLastFrameNative { get; set; }
        }

        private readonly DkmProcess _process;
        private readonly PythonRuntimeInfo _pyrtInfo;

        public CallStackFilter(DkmProcess process) {
            _process = process;
            _pyrtInfo = process.GetPythonRuntimeInfo();
        }

        public DkmStackWalkFrame[] FilterNextFrame(DkmStackContext stackContext, DkmStackWalkFrame nativeFrame) {
            var nativeModuleInstance = nativeFrame.ModuleInstance;
            if (nativeModuleInstance == _pyrtInfo.DLLs.DebuggerHelper) {
                return DebuggerOptions.ShowNativePythonFrames ? new[] { nativeFrame } : new DkmStackWalkFrame[0];
            }

            var result = new List<DkmStackWalkFrame>();
            var stackWalkData = stackContext.GetDataItem<StackWalkContextData>();
            if (stackWalkData == null) {
                stackWalkData = new StackWalkContextData();
                stackContext.SetDataItem(DkmDataCreationDisposition.CreateNew, stackWalkData);
            }
            bool? wasLastFrameNative = stackWalkData.IsLastFrameNative;

            if (nativeModuleInstance != _pyrtInfo.DLLs.Python && nativeModuleInstance != _pyrtInfo.DLLs.CTypes) {
                stackWalkData.IsLastFrameNative = true;
                if (wasLastFrameNative == false) {
                    result.Add(DkmStackWalkFrame.Create(nativeFrame.Thread, null, nativeFrame.FrameBase, nativeFrame.FrameSize,
                        DkmStackWalkFrameFlags.NonuserCode, "[Native to Python Transition]", null, null));
                } else {
                    stackWalkData.IsLastFrameNative = true;
                }
                result.Add(nativeFrame);
                return result.ToArray();
            } else {
                stackWalkData.IsLastFrameNative = false;
                if (wasLastFrameNative == true) {
                    result.Add(DkmStackWalkFrame.Create(nativeFrame.Thread, null, nativeFrame.FrameBase, nativeFrame.FrameSize,
                        DkmStackWalkFrameFlags.NonuserCode, "[Python to Native Transition]", null, null));
                }
            }

            var pythonFrame = PyFrameObject.TryCreate(nativeFrame);
            if (pythonFrame == null) {
                if (DebuggerOptions.ShowNativePythonFrames) {
                    result.Add(nativeFrame);
                }
                return result.ToArray();
            }

            PyCodeObject code = pythonFrame.f_code.Read();
            var loc = new SourceLocation(
                code.co_filename.Read().ToStringOrNull(),
                pythonFrame.f_lineno.Read(),
                code.co_name.Read().ToStringOrNull(),
                nativeFrame.InstructionAddress as DkmNativeInstructionAddress);

            var pythonRuntime = _process.GetPythonRuntimeInstance();
            var pythonModuleInstances = pythonRuntime.GetModuleInstances().OfType<DkmCustomModuleInstance>();
            var pyModuleInstance = pythonModuleInstances.Where(m => m.FullName == loc.FileName).FirstOrDefault();
            if (pyModuleInstance == null) {
                pyModuleInstance = pythonModuleInstances.Single(m => m.Module.Id.Mvid == Guids.UnknownPythonModuleGuid);
            }

            var encodedLocation = loc.Encode();
            var instrAddr = DkmCustomInstructionAddress.Create(pythonRuntime, pyModuleInstance, encodedLocation, 0, encodedLocation, null);
            var frame = DkmStackWalkFrame.Create(
                nativeFrame.Thread,
                instrAddr,
                nativeFrame.FrameBase,
                nativeFrame.FrameSize,
                DkmStackWalkFrameFlags.None,
                null,
                nativeFrame.Registers,
                nativeFrame.Annotations);
            result.Add(frame);

            if (DebuggerOptions.ShowNativePythonFrames) {
                result.Add(nativeFrame);
            }
            return result.ToArray();
        }

        public void GetFrameName(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmVariableInfoFlags argumentFlags, DkmCompletionRoutine<DkmGetFrameNameAsyncResult> completionRoutine) {
            var insAddr = frame.InstructionAddress as DkmCustomInstructionAddress;
            if (insAddr == null) {
                Debug.Fail("GetFrameName called on a Python frame without a proper instruction address.");
                throw new InvalidOperationException();
            }

            var loc = new SourceLocation(insAddr.AdditionalData, frame.Process);
            completionRoutine(new DkmGetFrameNameAsyncResult(loc.FunctionName));
        }

        public void GetFrameReturnType(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmCompletionRoutine<DkmGetFrameReturnTypeAsyncResult> completionRoutine) {
            completionRoutine(new DkmGetFrameReturnTypeAsyncResult(null));
        }
    }
}

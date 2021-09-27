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

using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;

namespace Microsoft.PythonTools.Debugger.Concord
{
    internal class CallStackFilter : DkmDataItem
    {
        private class StackWalkContextData : DkmDataItem
        {
            public bool? IsLastFrameNative { get; set; }
        }

        private readonly DkmProcess _process;
        private readonly PythonRuntimeInfo _pyrtInfo;

        public CallStackFilter(DkmProcess process)
        {
            _process = process;
            _pyrtInfo = process.GetPythonRuntimeInfo();
        }

        public DkmStackWalkFrame[] FilterNextFrame(DkmStackContext stackContext, DkmStackWalkFrame nativeFrame)
        {
            PyFrameObject pythonFrame = null;
            var nativeModuleInstance = nativeFrame.ModuleInstance;
            if (nativeModuleInstance == _pyrtInfo.DLLs.DebuggerHelper)
            {
                if (_pyrtInfo.LanguageVersion < PythonLanguageVersion.V36 ||
                    (pythonFrame = PyFrameObject.TryCreate(nativeFrame)) == null)
                {
                    return DebuggerOptions.ShowNativePythonFrames ? new[] { nativeFrame } : new DkmStackWalkFrame[0];
                }
            }

            var result = new List<DkmStackWalkFrame>();

            if (pythonFrame == null)
            {
                var stackWalkData = stackContext.GetDataItem<StackWalkContextData>();
                if (stackWalkData == null)
                {
                    stackWalkData = new StackWalkContextData();
                    stackContext.SetDataItem(DkmDataCreationDisposition.CreateNew, stackWalkData);
                }
                bool? wasLastFrameNative = stackWalkData.IsLastFrameNative;

                if (nativeModuleInstance != _pyrtInfo.DLLs.Python && nativeModuleInstance != _pyrtInfo.DLLs.CTypes)
                {
                    stackWalkData.IsLastFrameNative = true;
                    if (wasLastFrameNative == false)
                    {
                        result.Add(DkmStackWalkFrame.Create(nativeFrame.Thread, null, nativeFrame.FrameBase, nativeFrame.FrameSize,
                            DkmStackWalkFrameFlags.NonuserCode, Strings.DebugCallStackNativeToPythonTransition, null, null));
                    }
                    else
                    {
                        stackWalkData.IsLastFrameNative = true;
                    }
                    result.Add(nativeFrame);
                    return result.ToArray();
                }
                else
                {
                    stackWalkData.IsLastFrameNative = false;
                    if (wasLastFrameNative == true)
                    {
                        result.Add(DkmStackWalkFrame.Create(nativeFrame.Thread, null, nativeFrame.FrameBase, nativeFrame.FrameSize,
                            DkmStackWalkFrameFlags.NonuserCode, Strings.DebugCallStackPythonToNativeTransition, null, null));
                    }
                }

                pythonFrame = PyFrameObject.TryCreate(nativeFrame);
            }
            if (pythonFrame == null)
            {
                if (DebuggerOptions.ShowNativePythonFrames)
                {
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
            if (pyModuleInstance == null)
            {
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

            if (DebuggerOptions.ShowNativePythonFrames)
            {
                result.Add(nativeFrame);
            }
            return result.ToArray();
        }

        public void GetFrameName(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmVariableInfoFlags argumentFlags, DkmCompletionRoutine<DkmGetFrameNameAsyncResult> completionRoutine)
        {
            var insAddr = frame.InstructionAddress as DkmCustomInstructionAddress;
            if (insAddr == null)
            {
                Debug.Fail("GetFrameName called on a Python frame without a proper instruction address.");
                throw new InvalidOperationException();
            }

            var loc = new SourceLocation(insAddr.AdditionalData, frame.Process);
            completionRoutine(new DkmGetFrameNameAsyncResult(loc.FunctionName));
        }

        public void GetFrameReturnType(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmCompletionRoutine<DkmGetFrameReturnTypeAsyncResult> completionRoutine)
        {
            completionRoutine(new DkmGetFrameReturnTypeAsyncResult(null));
        }
    }
}

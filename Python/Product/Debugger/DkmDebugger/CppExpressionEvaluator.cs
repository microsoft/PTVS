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
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class CppExpressionEvaluator {
        private const uint Timeout = 200;

        public static readonly DkmLanguage CppLanguage = DkmLanguage.Create("C++", new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.CppLanguageGuid));

        private readonly DkmProcess _process;
        private readonly DkmStackWalkFrame _nativeFrame;
        private readonly DkmInspectionContext _cppInspectionContext;

        public CppExpressionEvaluator(DkmInspectionContext inspectionContext, DkmStackWalkFrame stackFrame) {
            _process = stackFrame.Process;
            var thread = stackFrame.Thread;

            if (stackFrame.InstructionAddress is DkmNativeInstructionAddress) {
                _nativeFrame = stackFrame;
            } else {
                var customAddr = stackFrame.InstructionAddress as DkmCustomInstructionAddress;
                if (customAddr == null) {
                    throw new ArgumentException();
                }

                var loc = new SourceLocation(customAddr.AdditionalData, _process);
                if (loc.NativeAddress == null) {
                    throw new ArgumentException();
                }

                _nativeFrame = DkmStackWalkFrame.Create(thread, loc.NativeAddress, stackFrame.FrameBase, stackFrame.FrameSize,
                    DkmStackWalkFrameFlags.None, null, stackFrame.Registers, null);
            }

            _cppInspectionContext = DkmInspectionContext.Create(inspectionContext.InspectionSession, _process.GetNativeRuntimeInstance(), thread, Timeout,
                DkmEvaluationFlags.TreatAsExpression | DkmEvaluationFlags.NoSideEffects, DkmFuncEvalFlags.None, inspectionContext.Radix, CppLanguage, null);
        }

        public CppExpressionEvaluator(DkmThread thread, ulong frameBase, ulong vframe) {
            _process = thread.Process;

            var inspectionSession = DkmInspectionSession.Create(_process, null);
            _cppInspectionContext = DkmInspectionContext.Create(inspectionSession, _process.GetNativeRuntimeInstance(), thread, Timeout,
                DkmEvaluationFlags.TreatAsExpression | DkmEvaluationFlags.NoSideEffects, DkmFuncEvalFlags.None, 10, CppLanguage, null);

            const int CV_ALLREG_VFRAME = 0x00007536;
            var vframeReg = DkmUnwoundRegister.Create(CV_ALLREG_VFRAME, new ReadOnlyCollection<byte>(BitConverter.GetBytes(vframe)));
            var regs = thread.GetCurrentRegisters(new[] { vframeReg });
            var iaddr = _process.CreateNativeInstructionAddress(regs.GetInstructionPointer());
            _nativeFrame = DkmStackWalkFrame.Create(thread, iaddr, frameBase, 0, DkmStackWalkFrameFlags.None, null, regs, null);
        }

        public static string GetExpressionForObject(string moduleName, string typeName, ulong address, string tail = "") {
            string expr = string.Format("(*(::{0}*){1}ULL){2}", typeName, address, tail);
            if (moduleName != null) {
                expr = "{,," + moduleName + "}" + expr;
            }
            return expr;
        }

        public DkmEvaluationResult TryEvaluate(string expr) {
            using (var cppExpr = DkmLanguageExpression.Create(CppLanguage, DkmEvaluationFlags.NoSideEffects, expr, null)) {
                DkmEvaluationResult cppEvalResult = null;
                var cppWorkList = DkmWorkList.Create(null);
                _cppInspectionContext.EvaluateExpression(cppWorkList, cppExpr, _nativeFrame, (result) => {
                    cppEvalResult = result.ResultObject;
                });
                cppWorkList.Execute();
                return cppEvalResult;
            }
        }

        public DkmEvaluationResult TryEvaluateObject(string moduleName, string typeName, ulong address, string tail = "") {
            return TryEvaluate(GetExpressionForObject(moduleName, typeName, address, tail));
        }

        public string Evaluate(string expr) {
            var er = TryEvaluate(expr);
            var ser = er as DkmSuccessEvaluationResult;
            if (ser == null) {
                throw new CppEvaluationException(er);
            }
            return ser.Value;
        }

        public int EvaluateInt32(string expr) {
            try {
                return int.Parse(Evaluate("(__int32)(" + expr + ")"));
            } catch (FormatException) {
                throw new CppEvaluationException();
            }
        }

        public ulong EvaluateUInt64(string expr) {
            try {
                return ulong.Parse(Evaluate("(unsigned __int64)(" + expr + ")"));
            } catch (FormatException) {
                throw new CppEvaluationException();
            }
        }

        public ulong EvaluateUInt64(string format, object arg0) {
            return EvaluateUInt64(string.Format(format, arg0));
        }

        public ulong EvaluateUInt64(string format, object arg0, object arg1) {
            return EvaluateUInt64(string.Format(format, arg0, arg1));
        }

        public ulong EvaluateReturnValueUInt64() {
            return EvaluateUInt64(_process.Is64Bit() ? "@rax" : "@eax");
        }
    }

    [Serializable]
    sealed class CppEvaluationException : Exception {
        public DkmEvaluationResult EvaluationResult { get; set; }

        public CppEvaluationException(DkmEvaluationResult evalResult = null) {
            EvaluationResult = evalResult;
        }
    }
}

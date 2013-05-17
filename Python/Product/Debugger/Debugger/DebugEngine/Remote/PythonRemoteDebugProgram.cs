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
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteDebugProgram : IDebugProgram2 {

        private readonly PythonRemoteDebugProcess _process;
        private readonly Guid _guid = Guid.NewGuid();

        public PythonRemoteDebugProgram(PythonRemoteDebugProcess process) {
            this._process = process;
        }

        public PythonRemoteDebugProcess DebugProcess {
            get { return _process; }
        }

        public int Attach(IDebugEventCallback2 pCallback) {
            throw new NotImplementedException();
        }

        public int CanDetach() {
            throw new NotImplementedException();
        }

        public int CauseBreak() {
            throw new NotImplementedException();
        }

        public int Continue(IDebugThread2 pThread) {
            throw new NotImplementedException();
        }

        public int Detach() {
            throw new NotImplementedException();
        }

        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum) {
            throw new NotImplementedException();
        }

        public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety) {
            throw new NotImplementedException();
        }

        public int EnumModules(out IEnumDebugModules2 ppEnum) {
            throw new NotImplementedException();
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum) {
            throw new NotImplementedException();
        }

        public int Execute() {
            throw new NotImplementedException();
        }

        public int GetDebugProperty(out IDebugProperty2 ppProperty) {
            throw new NotImplementedException();
        }

        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream) {
            throw new NotImplementedException();
        }

        public int GetENCUpdate(out object ppUpdate) {
            throw new NotImplementedException();
        }

        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine) {
            pguidEngine = AD7Engine.DebugEngineGuid;
            pbstrEngine = null;
            return 0;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
            throw new NotImplementedException();
        }

        public int GetName(out string pbstrName) {
            pbstrName = null;
            return 0;
        }

        public int GetProcess(out IDebugProcess2 ppProcess) {
            ppProcess = _process;
            return 0;
        }

        public int GetProgramId(out Guid pguidProgramId) {
            pguidProgramId = _guid;
            return 0;
        }

        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step) {
            throw new NotImplementedException();
        }

        public int Terminate() {
            throw new NotImplementedException();
        }

        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl) {
            throw new NotImplementedException();
        }
    }
}

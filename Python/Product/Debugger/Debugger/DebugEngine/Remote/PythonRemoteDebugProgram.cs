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

using Microsoft.PythonTools.Debugger.DebugEngine;

namespace Microsoft.PythonTools.Debugger.Remote
{
	internal class PythonRemoteDebugProgram : IDebugProgram2
	{
		public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
		public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

		private readonly PythonRemoteDebugProcess _process;
		private readonly Guid _guid = Guid.NewGuid();

		public PythonRemoteDebugProgram(PythonRemoteDebugProcess process)
		{
			this._process = process;
		}

		public PythonRemoteDebugProcess DebugProcess
		{
			get { return _process; }
		}

		public int Attach(IDebugEventCallback2 pCallback)
		{
			throw new NotImplementedException();
		}

		public int CanDetach()
		{
			throw new NotImplementedException();
		}

		public int CauseBreak()
		{
			throw new NotImplementedException();
		}

		public int Continue(IDebugThread2 pThread)
		{
			throw new NotImplementedException();
		}

		public int Detach()
		{
			throw new NotImplementedException();
		}

		public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
		{
			throw new NotImplementedException();
		}

		public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety)
		{
			throw new NotImplementedException();
		}

		public int EnumModules(out IEnumDebugModules2 ppEnum)
		{
			throw new NotImplementedException();
		}

		public int EnumThreads(out IEnumDebugThreads2 ppEnum)
		{
			throw new NotImplementedException();
		}

		public int Execute()
		{
			throw new NotImplementedException();
		}

		public int GetDebugProperty(out IDebugProperty2 ppProperty)
		{
			throw new NotImplementedException();
		}

		public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream)
		{
			throw new NotImplementedException();
		}

		public int GetENCUpdate(out object ppUpdate)
		{
			throw new NotImplementedException();
		}

		public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
		{
			pguidEngine = PythonDebugOptionsServiceHelper.Options.UseLegacyDebugger ?
				AD7Engine.DebugEngineGuid :
				VSCodeDebugEngine;
			pbstrEngine = null;

			return 0;
		}

		public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
		{
			throw new NotImplementedException();
		}

		public int GetName(out string pbstrName)
		{
			pbstrName = null;
			return 0;
		}

		public int GetProcess(out IDebugProcess2 ppProcess)
		{
			ppProcess = _process;
			return 0;
		}

		public int GetProgramId(out Guid pguidProgramId)
		{
			pguidProgramId = _guid;
			return 0;
		}

		public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step)
		{
			throw new NotImplementedException();
		}

		public int Terminate()
		{
			throw new NotImplementedException();
		}

		public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
		{
			throw new NotImplementedException();
		}
	}
}

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

namespace Microsoft.PythonTools.Debugger.DebugEngine
{
	static class EngineUtils
	{
		public static void CheckOk(int hr)
		{
			if (hr != 0)
			{
				Marshal.ThrowExceptionForHR(hr);
			}
		}

		public static void RequireOk(int hr)
		{
			if (hr != 0)
			{
				throw new InvalidOperationException();
			}
		}

		public static int GetProcessId(IDebugProcess2 process)
		{
			AD_PROCESS_ID[] pid = new AD_PROCESS_ID[1];
			EngineUtils.RequireOk(process.GetPhysicalProcessId(pid));

			if (pid[0].ProcessIdType != (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
			{
				return 0;
			}

			return (int)pid[0].dwProcessId;
		}

		public static int GetProcessId(IDebugProgram2 program)
		{
			RequireOk(program.GetProcess(out IDebugProcess2 process));

			return GetProcessId(process);
		}
	}
}

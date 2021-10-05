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
	// This class represents the information that describes a bound breakpoint.
	class AD7BreakpointResolution : IDebugBreakpointResolution2
	{
		private readonly AD7Engine m_engine;
		private readonly PythonBreakpoint m_address;
		private readonly AD7DocumentContext m_documentContext;

		public AD7BreakpointResolution(AD7Engine engine, PythonBreakpoint address, AD7DocumentContext documentContext)
		{
			m_engine = engine;
			m_address = address;
			m_documentContext = documentContext;
		}

		#region IDebugBreakpointResolution2 Members

		// Gets the type of the breakpoint represented by this resolution. 
		int IDebugBreakpointResolution2.GetBreakpointType(enum_BP_TYPE[] pBPType)
		{
			// The sample engine only supports code breakpoints.
			pBPType[0] = enum_BP_TYPE.BPT_CODE;
			return VSConstants.S_OK;
		}

		// Gets the breakpoint resolution information that describes this breakpoint.
		int IDebugBreakpointResolution2.GetResolutionInfo(enum_BPRESI_FIELDS dwFields, BP_RESOLUTION_INFO[] pBPResolutionInfo)
		{
			if ((dwFields & enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION) != 0)
			{
				// The sample engine only supports code breakpoints.
				BP_RESOLUTION_LOCATION location = new BP_RESOLUTION_LOCATION();
				location.bpType = (uint)enum_BP_TYPE.BPT_CODE;

				// The debugger will not QI the IDebugCodeContex2 interface returned here. We must pass the pointer
				// to IDebugCodeContex2 and not IUnknown.
				AD7MemoryAddress codeContext = new AD7MemoryAddress(m_engine, m_address.Filename, (uint)m_address.LineNo);
				codeContext.SetDocumentContext(m_documentContext);
				location.unionmember1 = Marshal.GetComInterfaceForObject(codeContext, typeof(IDebugCodeContext2));
				pBPResolutionInfo[0].bpResLocation = location;
				pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION;

			}

			if ((dwFields & enum_BPRESI_FIELDS.BPRESI_PROGRAM) != 0)
			{
				pBPResolutionInfo[0].pProgram = (IDebugProgram2)m_engine;
				pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_PROGRAM;
			}

			return VSConstants.S_OK;
		}

		#endregion
	}

	class AD7ErrorBreakpointResolution : IDebugErrorBreakpointResolution2
	{
		#region IDebugErrorBreakpointResolution2 Members

		int IDebugErrorBreakpointResolution2.GetBreakpointType(enum_BP_TYPE[] pBPType)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		int IDebugErrorBreakpointResolution2.GetResolutionInfo(enum_BPERESI_FIELDS dwFields, BP_ERROR_RESOLUTION_INFO[] pErrorResolutionInfo)
		{
			if (((uint)dwFields & (uint)enum_BPERESI_FIELDS.BPERESI_BPRESLOCATION) != 0) { }
			if (((uint)dwFields & (uint)enum_BPERESI_FIELDS.BPERESI_PROGRAM) != 0) { }
			if (((uint)dwFields & (uint)enum_BPERESI_FIELDS.BPERESI_THREAD) != 0) { }
			if (((uint)dwFields & (uint)enum_BPERESI_FIELDS.BPERESI_MESSAGE) != 0) { }
			if (((uint)dwFields & (uint)enum_BPERESI_FIELDS.BPERESI_TYPE) != 0) { }

			throw new Exception("The method or operation is not implemented.");
		}

		#endregion
	}

}

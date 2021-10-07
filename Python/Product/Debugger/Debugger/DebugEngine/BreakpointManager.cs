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
	// This class manages breakpoints for the engine. 
	internal class BreakpointManager
	{
		private readonly AD7Engine m_engine;
		private readonly List<AD7PendingBreakpoint> m_pendingBreakpoints;
		private readonly Dictionary<PythonBreakpoint, AD7BoundBreakpoint> _breakpointMap = new Dictionary<PythonBreakpoint, AD7BoundBreakpoint>();

		public BreakpointManager(AD7Engine engine)
		{
			m_engine = engine;
			m_pendingBreakpoints = new System.Collections.Generic.List<AD7PendingBreakpoint>();
		}

		// A helper method used to construct a new pending breakpoint.
		public void CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
		{
			AD7PendingBreakpoint pendingBreakpoint = new AD7PendingBreakpoint(pBPRequest, m_engine, this);
			ppPendingBP = (IDebugPendingBreakpoint2)pendingBreakpoint;
			m_pendingBreakpoints.Add(pendingBreakpoint);
		}

		// Called from the engine's detach method to remove the debugger's breakpoint instructions.
		public void ClearBoundBreakpoints()
		{
			foreach (AD7PendingBreakpoint pendingBreakpoint in m_pendingBreakpoints)
			{
				pendingBreakpoint.ClearBoundBreakpoints();
			}
		}

		public void AddBoundBreakpoint(PythonBreakpoint breakpoint, AD7BoundBreakpoint boundBreakpoint)
		{
			_breakpointMap[breakpoint] = boundBreakpoint;
		}

		public void RemoveBoundBreakpoint(PythonBreakpoint breakpoint)
		{
			_breakpointMap.Remove(breakpoint);
		}

		public AD7BoundBreakpoint GetBreakpoint(PythonBreakpoint breakpoint)
		{
			return _breakpointMap[breakpoint];
		}
	}
}

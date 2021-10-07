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


// This file contains the various event objects that are sent to the debugger from the sample engine via IDebugEventCallback2::Event.
// These are used in EngineCallback.cs.
// The events are how the engine tells the debugger about what is happening in the debuggee process. 
// There are three base classe the other events derive from: AD7AsynchronousEvent, AD7StoppingEvent, and AD7SynchronousEvent. These 
// each implement the IDebugEvent2.GetAttributes method for the type of event they represent. 
// Most events sent the debugger are asynchronous events.

namespace Microsoft.PythonTools.Debugger.DebugEngine
{
	#region Event base classes

	internal class AD7AsynchronousEvent : IDebugEvent2
	{
		public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;

		int IDebugEvent2.GetAttributes(out uint eventAttributes)
		{
			eventAttributes = Attributes;
			return VSConstants.S_OK;
		}
	}

	internal class AD7StoppingEvent : IDebugEvent2
	{
		public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNC_STOP;

		int IDebugEvent2.GetAttributes(out uint eventAttributes)
		{
			eventAttributes = Attributes;
			return VSConstants.S_OK;
		}
	}

	internal class AD7SynchronousEvent : IDebugEvent2
	{
		public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS;

		int IDebugEvent2.GetAttributes(out uint eventAttributes)
		{
			eventAttributes = Attributes;
			return VSConstants.S_OK;
		}
	}

	#endregion

	// The debug engine (DE) sends this interface to the session debug manager (SDM) when an instance of the DE is created.
	internal sealed class AD7EngineCreateEvent : AD7AsynchronousEvent, IDebugEngineCreateEvent2
	{
		public const string IID = "FE5B734C-759D-4E59-AB04-F103343BDD06";
		private IDebugEngine2 m_engine;

		private AD7EngineCreateEvent(AD7Engine engine)
		{
			m_engine = engine;
		}

		public static void Send(AD7Engine engine)
		{
			AD7EngineCreateEvent eventObject = new AD7EngineCreateEvent(engine);
			engine.Send(eventObject, IID, null, null);
		}

		int IDebugEngineCreateEvent2.GetEngine(out IDebugEngine2 engine)
		{
			engine = m_engine;
			return VSConstants.S_OK;
		}
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a program is attached to.
	internal sealed class AD7ProgramCreateEvent : AD7AsynchronousEvent, IDebugProgramCreateEvent2
	{
		public const string IID = "96CD11EE-ECD4-4E89-957E-B5D496FC4139";

		internal static void Send(AD7Engine engine)
		{
			AD7ProgramCreateEvent eventObject = new AD7ProgramCreateEvent();
			engine.Send(eventObject, IID, null);
		}
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a program is attached to.
	internal sealed class AD7ExpressionEvaluationCompleteEvent : AD7AsynchronousEvent, IDebugExpressionEvaluationCompleteEvent2
	{
		public const string IID = "C0E13A85-238A-4800-8315-D947C960A843";
		private readonly IDebugExpression2 _expression;
		private readonly IDebugProperty2 _property;

		public AD7ExpressionEvaluationCompleteEvent(IDebugExpression2 expression, IDebugProperty2 property)
		{
			_expression = expression;
			_property = property;
		}

		#region IDebugExpressionEvaluationCompleteEvent2 Members

		public int GetExpression(out IDebugExpression2 ppExpr)
		{
			ppExpr = _expression;
			return VSConstants.S_OK;
		}

		public int GetResult(out IDebugProperty2 ppResult)
		{
			ppResult = _property;
			return VSConstants.S_OK;
		}

		#endregion
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a module is loaded or unloaded.
	internal sealed class AD7ModuleLoadEvent : AD7AsynchronousEvent, IDebugModuleLoadEvent2
	{
		public const string IID = "989DB083-0D7C-40D1-A9D9-921BF611A4B2";
		private readonly AD7Module m_module;
		private readonly bool m_fLoad;

		public AD7ModuleLoadEvent(AD7Module module, bool fLoad)
		{
			m_module = module;
			m_fLoad = fLoad;
		}

		int IDebugModuleLoadEvent2.GetModule(out IDebugModule2 module, ref string debugMessage, ref int fIsLoad)
		{
			module = m_module;

			if (m_fLoad)
			{
				debugMessage = null; //String.Concat("Loaded '", m_module.DebuggedModule.Name, "'");
				fIsLoad = 1;
			}
			else
			{
				debugMessage = null; // String.Concat("Unloaded '", m_module.DebuggedModule.Name, "'");
				fIsLoad = 0;
			}

			return VSConstants.S_OK;
		}
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a program has run to completion
	// or is otherwise destroyed.
	internal sealed class AD7ProgramDestroyEvent : AD7SynchronousEvent, IDebugProgramDestroyEvent2
	{
		public const string IID = "E147E9E3-6440-4073-A7B7-A65592C714B5";
		private readonly uint m_exitCode;
		public AD7ProgramDestroyEvent(uint exitCode)
		{
			m_exitCode = exitCode;
		}

		#region IDebugProgramDestroyEvent2 Members

		int IDebugProgramDestroyEvent2.GetExitCode(out uint exitCode)
		{
			exitCode = m_exitCode;

			return VSConstants.S_OK;
		}

		#endregion
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a thread is created in a program being debugged.
	internal sealed class AD7ThreadCreateEvent : AD7AsynchronousEvent, IDebugThreadCreateEvent2
	{
		public const string IID = "2090CCFC-70C5-491D-A5E8-BAD2DD9EE3EA";
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a thread has exited.
	internal sealed class AD7ThreadDestroyEvent : AD7AsynchronousEvent, IDebugThreadDestroyEvent2
	{
		public const string IID = "2C3B7532-A36F-4A6E-9072-49BE649B8541";
		private readonly uint m_exitCode;
		public AD7ThreadDestroyEvent(uint exitCode)
		{
			m_exitCode = exitCode;
		}

		#region IDebugThreadDestroyEvent2 Members

		int IDebugThreadDestroyEvent2.GetExitCode(out uint exitCode)
		{
			exitCode = m_exitCode;

			return VSConstants.S_OK;
		}

		#endregion
	}

	// This interface is sent by the debug engine (DE) to the session debug manager (SDM)
	// when a program is loaded, but before any code is executed.
	internal sealed class AD7LoadCompleteEvent : IDebugEvent2, IDebugLoadCompleteEvent2
	{
		public const string IID = "B1844850-1349-45D4-9F12-495212F5EB0B";

		private readonly uint _attributes;

		public AD7LoadCompleteEvent(IDebugThread2 thread)
		{
			if (thread == null)
			{
				_attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;
			}
			else
			{
				_attributes = (uint)enum_EVENTATTRIBUTES.EVENT_STOPPING;
			}
		}

		internal static void Send(AD7Engine engine, IDebugThread2 thread)
		{
			AD7LoadCompleteEvent eventObject = new AD7LoadCompleteEvent(thread);
			engine.Send(eventObject, IID, thread);
		}

		public int GetAttributes(out uint pdwAttrib)
		{
			pdwAttrib = _attributes;
			return VSConstants.S_OK;
		}
	}

	// This interface tells the session debug manager (SDM) that an asynchronous break has been successfully completed.
	internal sealed class AD7AsyncBreakCompleteEvent : AD7StoppingEvent, IDebugBreakEvent2
	{
		public const string IID = "c7405d1d-e24b-44e0-b707-d8a5a4e1641b";
	}

	// This interface tells the session debug manager (SDM) that an asynchronous break has been successfully completed.
	internal sealed class AD7SteppingCompleteEvent : AD7StoppingEvent, IDebugStepCompleteEvent2
	{
		public const string IID = "0F7F24C1-74D9-4EA6-A3EA-7EDB2D81441D";
	}

	// This interface is sent when a pending breakpoint has been bound in the debuggee.
	internal sealed class AD7BreakpointBoundEvent : AD7AsynchronousEvent, IDebugBreakpointBoundEvent2
	{
		public const string IID = "1dddb704-cf99-4b8a-b746-dabb01dd13a0";

		private readonly AD7PendingBreakpoint m_pendingBreakpoint;
		private readonly AD7BoundBreakpoint m_boundBreakpoint;

		public AD7BreakpointBoundEvent(AD7PendingBreakpoint pendingBreakpoint, AD7BoundBreakpoint boundBreakpoint)
		{
			m_pendingBreakpoint = pendingBreakpoint;
			m_boundBreakpoint = boundBreakpoint;
		}

		#region IDebugBreakpointBoundEvent2 Members

		int IDebugBreakpointBoundEvent2.EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum)
		{
			IDebugBoundBreakpoint2[] boundBreakpoints = new IDebugBoundBreakpoint2[1];
			boundBreakpoints[0] = m_boundBreakpoint;
			ppEnum = new AD7BoundBreakpointsEnum(boundBreakpoints);
			return VSConstants.S_OK;
		}

		int IDebugBreakpointBoundEvent2.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBP)
		{
			ppPendingBP = m_pendingBreakpoint;
			return VSConstants.S_OK;
		}

		#endregion
	}

	// This interface is sent when the entry point has been hit.
	internal sealed class AD7EntryPointEvent : AD7StoppingEvent, IDebugEntryPointEvent2
	{
		public const string IID = "e8414a3e-1642-48ec-829e-5f4040e16da9";
	}

	// This Event is sent when a breakpoint is hit in the debuggee
	internal sealed class AD7BreakpointEvent : AD7StoppingEvent, IDebugBreakpointEvent2
	{
		public const string IID = "501C1E21-C557-48B8-BA30-A1EAB0BC4A74";
		private IEnumDebugBoundBreakpoints2 m_boundBreakpoints;

		public AD7BreakpointEvent(IEnumDebugBoundBreakpoints2 boundBreakpoints)
		{
			m_boundBreakpoints = boundBreakpoints;
		}

		#region IDebugBreakpointEvent2 Members

		int IDebugBreakpointEvent2.EnumBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum)
		{
			ppEnum = m_boundBreakpoints;
			return VSConstants.S_OK;
		}

		#endregion
	}

	internal sealed class AD7DebugOutputStringEvent2 : AD7AsynchronousEvent, IDebugOutputStringEvent2
	{
		public const string IID = "569C4BB1-7B82-46FC-AE28-4536DDAD753E";
		private readonly string _output;

		public AD7DebugOutputStringEvent2(string output)
		{
			_output = output;
		}
		#region IDebugOutputStringEvent2 Members

		public int GetString(out string pbstrString)
		{
			pbstrString = _output;
			return VSConstants.S_OK;
		}

		#endregion
	}

	internal sealed class AD7CustomEvent : IDebugEvent2, IDebugCustomEvent110
	{
		public const string IID = "2615D9BC-1948-4D21-81EE-7A963F20CF59";
		private readonly VsComponentMessage _message;

		public AD7CustomEvent(VsComponentMessage message)
		{
			_message = message;
		}

		public AD7CustomEvent(VsPackageMessage message, object param1 = null, object param2 = null)
			: this(new VsComponentMessage { MessageCode = (uint)message, Parameter1 = param1, Parameter2 = param2 })
		{
		}

		int IDebugEvent2.GetAttributes(out uint eventAttributes)
		{
			eventAttributes = (uint)(enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS | enum_EVENTATTRIBUTES.EVENT_IMMEDIATE);
			return VSConstants.S_OK;
		}

		int IDebugCustomEvent110.GetCustomEventInfo(out Guid guidVSService, VsComponentMessage[] message)
		{
			guidVSService = Guids.CustomDebuggerEventHandlerGuid;
			message[0] = _message;
			return VSConstants.S_OK;
		}
	}
}

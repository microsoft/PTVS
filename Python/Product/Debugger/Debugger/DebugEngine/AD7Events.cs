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

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

// This file contains the various event objects that are sent to the debugger from the sample engine via IDebugEventCallback2::Event.
// These are used in EngineCallback.cs.
// The events are how the engine tells the debugger about what is happening in the debuggee process. 
// There are three base classe the other events derive from: AD7AsynchronousEvent, AD7StoppingEvent, and AD7SynchronousEvent. These 
// each implement the IDebugEvent2.GetAttributes method for the type of event they represent. 
// Most events sent the debugger are asynchronous events.


namespace Microsoft.PythonTools.Debugger.DebugEngine {
    #region Event base classes

    class AD7AsynchronousEvent : IDebugEvent2 {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;

        int IDebugEvent2.GetAttributes(out uint eventAttributes) {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    class AD7StoppingEvent : IDebugEvent2 {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNC_STOP;

        int IDebugEvent2.GetAttributes(out uint eventAttributes) {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    class AD7SynchronousEvent : IDebugEvent2 {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS;

        int IDebugEvent2.GetAttributes(out uint eventAttributes) {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    #endregion

    // The debug engine (DE) sends this interface to the session debug manager (SDM) when an instance of the DE is created.
    sealed class AD7EngineCreateEvent : AD7AsynchronousEvent, IDebugEngineCreateEvent2 {
        public const string IID = "FE5B734C-759D-4E59-AB04-F103343BDD06";
        private IDebugEngine2 m_engine;

        AD7EngineCreateEvent(AD7Engine engine) {
            m_engine = engine;
        }

        public static void Send(AD7Engine engine) {
            AD7EngineCreateEvent eventObject = new AD7EngineCreateEvent(engine);
            engine.Send(eventObject, IID, null, null);
        }

        int IDebugEngineCreateEvent2.GetEngine(out IDebugEngine2 engine) {
            engine = m_engine;
            return VSConstants.S_OK;
        }
    }

    // This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a program is attached to.
    sealed class AD7ProgramCreateEvent : AD7AsynchronousEvent, IDebugProgramCreateEvent2 {
        public const string IID = "96CD11EE-ECD4-4E89-957E-B5D496FC4139";

        internal static void Send(AD7Engine engine) {
            AD7ProgramCreateEvent eventObject = new AD7ProgramCreateEvent();
            engine.Send(eventObject, IID, null);
        }
    }

    // This interface is sent by the debug engine (DE) to the session debug manager (SDM)
    // when a program is loaded, but before any code is executed.
    sealed class AD7LoadCompleteEvent : IDebugEvent2, IDebugLoadCompleteEvent2 {
        public const string IID = "B1844850-1349-45D4-9F12-495212F5EB0B";

        private uint _attributes;

        public AD7LoadCompleteEvent(IDebugThread2 thread) {
            if (thread == null) {
                _attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;
            } else {
                _attributes = (uint)enum_EVENTATTRIBUTES.EVENT_STOPPING;
            }
        }

        internal static void Send(AD7Engine engine, IDebugThread2 thread) {
            var eventObject = new AD7LoadCompleteEvent(thread);
            engine.Send(eventObject, IID, thread);
        }

        public int GetAttributes(out uint pdwAttrib) {
            pdwAttrib = _attributes;
            return VSConstants.S_OK;
        }
    }


    sealed class AD7CustomEvent : IDebugEvent2, IDebugCustomEvent110 {
        public const string IID = "2615D9BC-1948-4D21-81EE-7A963F20CF59";
        private readonly VsComponentMessage _message;

        public AD7CustomEvent(VsComponentMessage message) {
            _message = message;
        }

        public AD7CustomEvent(VsPackageMessage message, object param1 = null, object param2 = null)
            : this(new VsComponentMessage { MessageCode = (uint)message, Parameter1 = param1, Parameter2 = param2 }) {
        }

        int IDebugEvent2.GetAttributes(out uint eventAttributes) {
            eventAttributes = (uint)(enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS | enum_EVENTATTRIBUTES.EVENT_IMMEDIATE);
            return VSConstants.S_OK;
        }

        int IDebugCustomEvent110.GetCustomEventInfo(out Guid guidVSService, VsComponentMessage[] message) {
            guidVSService = Guids.CustomDebuggerEventHandlerGuid;
            message[0] = _message;
            return VSConstants.S_OK;
        }
    }
}

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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Ipc.Json;

namespace Microsoft.PythonTools.Debugger {
    // IMPORTANT:
    // Names of all fields, commands and events must match the names in
    // ptvsd/debugger.py and ptvsd/attach_server.py
    internal static class LegacyDebuggerProtocol {
        public static readonly Dictionary<string, Type> RegisteredTypes = CollectCommands();

        private static Dictionary<string, Type> CollectCommands() {
            Dictionary<string, Type> all = new Dictionary<string, Type>();
            foreach (var type in typeof(LegacyDebuggerProtocol).GetNestedTypes()) {
                if (type.IsSubclassOf(typeof(Request))) {
                    var command = type.GetField("Command");
                    if (command != null) {
                        all["request." + (string) command.GetRawConstantValue()] = type;
                    }
                }
                else if (type.IsSubclassOf(typeof(Event))) {
                    var name = type.GetField("Name");
                    if (name != null) {
                        all["event." + (string) name.GetRawConstantValue()] = type;
                    }
                }
            }
            return all;
        }

        //////////////////////////////////////////////////////////////////////
        /// Requests
        //////////////////////////////////////////////////////////////////////

        public sealed class StepIntoRequest : GenericRequest {
            public const string Command = "legacyStepInto";

            public override string command => Command;

            public long threadId;
        }

        public sealed class StepOutRequest : GenericRequest {
            public const string Command = "legacyStepOut";

            public override string command => Command;

            public long threadId;
        }

        public sealed class StepOverRequest : GenericRequest {
            public const string Command = "legacyStepOver";

            public override string command => Command;

            public long threadId;
        }

        public sealed class BreakAllRequest : GenericRequest {
            public const string Command = "legacyBreakAll";

            public override string command => Command;
        }

        public sealed class SetBreakpointRequest : GenericRequest {
            public const string Command = "legacySetBreakpoint";

            public override string command => Command;

            public LanguageKind language;

            public int breakpointId;
            public int breakpointLineNo;
            public string breakpointFileName;
            public BreakpointConditionKind conditionKind;
            public string condition;
            public BreakpointPassCountKind passCountKind;
            public int passCount;
        }

        public sealed class RemoveBreakpointRequest : GenericRequest {
            public const string Command = "legacyRemoveBreakpoint";

            public override string command => Command;

            public LanguageKind language;
            public int breakpointId;
            public int breakpointLineNo;
            public string breakpointFileName;
        }

        public sealed class SetBreakpointConditionRequest : GenericRequest {
            public const string Command = "legacySetBreakpointCondition";

            public override string command => Command;

            public int breakpointId;
            public BreakpointConditionKind conditionKind;
            public string condition;
        }

        public sealed class SetBreakpointPassCountRequest : GenericRequest {
            public const string Command = "legacySetBreakpointPassCount";

            public override string command => Command;

            public int breakpointId;
            public BreakpointPassCountKind passCountKind;
            public int passCount;
        }

        public sealed class SetBreakpointHitCountRequest : GenericRequest {
            public const string Command = "legacySetBreakpointHitCount";

            public override string command => Command;

            public int breakpointId;
            public int hitCount;
        }

        public sealed class GetBreakpointHitCountRequest : Request<GetBreakpointHitCountResponse> {
            public const string Command = "legacyGetBreakpointHitCount";

            public override string command => Command;

            public int breakpointId;
        }

        public sealed class GetBreakpointHitCountResponse : Response {
            public int hitCount;
        }

        public sealed class ResumeAllRequest : GenericRequest {
            public const string Command = "legacyResumeAll";

            public override string command => Command;
        }

        public sealed class ResumeThreadRequest : GenericRequest {
            public const string Command = "legacyResumeThread";

            public override string command => Command;

            public long threadId;
        }

        public sealed class AutoResumeThreadRequest : GenericRequest {
            public const string Command = "legacyAutoResumeThread";

            public override string command => Command;

            public long threadId;
        }

        public sealed class ClearSteppingRequest : GenericRequest {
            public const string Command = "legacyClearStepping";

            public override string command => Command;

            public long threadId;
        }

        public sealed class ExecuteTextRequest : GenericRequest {
            public const string Command = "legacyExecuteText";

            public override string command => Command;

            public string text;
            public int executionId;
            public long threadId;
            public int frameId;
            public FrameKind frameKind;
            public ReprKind reprKind;
            public string moduleName;
            public bool printResult;
        }

        public sealed class DetachRequest : GenericRequest {
            public const string Command = "legacyDetach";

            public override string command => Command;
        }

        public sealed class SetExceptionInfoRequest : GenericRequest {
            public const string Command = "legacySetExceptionInfo";

            public override string command => Command;

            public int defaultBreakOnMode;
            public ExceptionInfo[] breakOn;
        }

        public sealed class LastAckRequest : GenericRequest {
            public const string Command = "legacyLastAck";

            public override string command => Command;
        }

        public sealed class EnumThreadFramesRequest : GenericRequest {
            public const string Command = "legacyGetThreadFrames";

            public override string command => Command;

            public long threadId;
        }

        public sealed class EnumChildrenRequest : GenericRequest {
            public const string Command = "legacyEnumChildren";

            public override string command => Command;

            public string text;
            public int executionId;
            public long threadId;
            public int frameId;
            public FrameKind frameKind;
        }

        public sealed class SetLineNumberRequest : Request<SetLineNumberResponse> {
            public const string Command = "legacySetLineNumber";

            public override string command => Command;

            public long threadId;
            public int frameId;
            public int lineNo;
        }

        public sealed class SetLineNumberResponse : Response {
            public int result;
            public long threadId;
            public int newLineNo;
        }

        // This request is in response to a RequestHandlersEvent.
        // Python process waits for this request after firing that event.
        public sealed class SetExceptionHandlerInfoRequest : GenericRequest {
            public const string Command = "legacySetExceptionHandlerInfo";

            public override string command => Command;

            public string fileName;
            public ExceptionHandlerStatement[] statements;
        }

        public sealed class RemoteDebuggerAuthenticateRequest : Request<RemoteDebuggerAuthenticateResponse> {
            public const string Command = "legacyRemoteDebuggerAuthenticate";

            public override string command => Command;

            public string debuggerName;
            public int debuggerProtocolVersion;
            public string clientSecret;
        }

        public sealed class RemoteDebuggerAuthenticateResponse : Response {
            public bool accepted;
        }

        public sealed class RemoteDebuggerInfoRequest : Request<RemoteDebuggerInfoResponse> {
            public const string Command = "legacyRemoteDebuggerInfo";

            public override string command => Command;
        }

        public sealed class RemoteDebuggerInfoResponse : Response {
            public int processId;
            public string executable;
            public string user;
            public string pythonVersion; // maybe split into its components, right now it's just for display, it's never parsed
        }

        public sealed class RemoteDebuggerAttachRequest : Request<RemoteDebuggerAttachResponse> {
            public const string Command = "legacyRemoteDebuggerAttach";

            public override string command => Command;

            public string debugOptions;
        }

        public sealed class RemoteDebuggerAttachResponse : Response {
            public bool accepted;
            public int processId;
            public int pythonMajor;
            public int pythonMinor;
            public int pythonMicro;
        }

        public sealed class ListReplModulesRequest : Request<ListReplModulesResponse> {
            public const string Command = "legacyListReplModules";

            public override string command => Command;
        }

        public sealed class ListReplModulesResponse : Response {
            public ModuleItem[] modules;
        }

        //////////////////////////////////////////////////////////////////////
        // Events
        //////////////////////////////////////////////////////////////////////

        public sealed class LocalConnectedEvent : Event {
            public const string Name = "legacyLocalConnected";

            public override string name => Name;

            public string processGuid;
            public int result;
        }

        public sealed class RemoteConnectedEvent : Event {
            public const string Name = "legacyRemoteConnected";

            public override string name => Name;

            public string debuggerName;
            public int debuggerProtocolVersion;
        }

        public sealed class DetachEvent : Event {
            public const string Name = "legacyDetach";

            public override string name => Name;
        }

        public sealed class LastEvent : Event {
            public const string Name = "legacyLast";

            public override string name => Name;
        }

        public sealed class RequestHandlersEvent : Event {
            public const string Name = "legacyRequestHandlers";

            public override string name => Name;

            public string fileName;
        }

        public sealed class ExceptionEvent : Event {
            public const string Name = "legacyException";

            public override string name => Name;

            public long threadId;
            public Dictionary<string, string> data;
        }

        public sealed class BreakpointHitEvent : Event {
            public const string Name = "legacyBreakpointHit";

            public override string name => Name;

            public int breakpointId;
            public long threadId;
        }

        public sealed class AsyncBreakEvent : Event {
            public const string Name = "legacyAsyncBreak";

            public override string name => Name;

            public long threadId;
        }

        public sealed class ThreadCreateEvent : Event {
            public const string Name = "legacyThreadCreate";

            public override string name => Name;

            public long threadId;
        }

        public sealed class ThreadExitEvent : Event {
            public const string Name = "legacyThreadExit";

            public override string name => Name;

            public long threadId;
        }

        public sealed class ModuleLoadEvent : Event {
            public const string Name = "legacyModuleLoad";

            public override string name => Name;

            public int moduleId;
            public string moduleFileName;
        }

        public sealed class StepDoneEvent : Event {
            public const string Name = "legacyStepDone";

            public override string name => Name;

            public long threadId;
        }

        public sealed class ProcessLoadEvent : Event {
            public const string Name = "legacyProcessLoad";

            public override string name => Name;

            public long threadId;
        }

        public sealed class BreakpointSetEvent : Event {
            public const string Name = "legacyBreakpointSet";

            public override string name => Name;

            public int breakpointId;
        }

        public sealed class BreakpointFailedEvent : Event {
            public const string Name = "legacyBreakpointFailed";

            public override string name => Name;

            public int breakpointId;
        }

        public sealed class DebuggerOutputEvent : Event {
            public const string Name = "legacyDebuggerOutput";

            public override string name => Name;

            public long threadId;
            public string output;
            public bool isStdOut;
        }

        public sealed class ExecutionResultEvent : Event {
            public const string Name = "legacyExecutionResult";

            public override string name => Name;

            public int executionId;
            public PythonObject obj;
        }

        public sealed class ExecutionExceptionEvent : Event {
            public const string Name = "legacyExecutionException";

            public override string name => Name;

            public int executionId;
            public string exceptionText;
        }

        public sealed class EnumChildrenEvent : Event {
            public const string Name = "legacyEnumChildrenResult";

            public override string name => Name;

            public int executionId;
            public EnumChildrenItem[] children;
        }

        public sealed class ThreadFrameListEvent : Event {
            public const string Name = "legacyThreadFrameList";

            public override string name => Name;

            public long threadId;
            public string threadName;
            public ThreadFrameItem[] threadFrames;
        }

        public sealed class ModulesChangedEvent : Event {
            public const string Name = "legacyModulesChanged";

            public override string name => Name;
        }

        //////////////////////////////////////////////////////////////////////
        /// Protocol types
        //////////////////////////////////////////////////////////////////////

        [Flags]
        public enum EvaluationResultFlags {
            None = 0,
            Expandable = 1,
            MethodCall = 2,
            SideEffects = 4,
            Raw = 8,
            HasRawRepr = 16,
        }

        public enum LanguageKind {
            Python = 0,
            Django = 1,
        }

        public enum FrameKind {
            None = 0,
            Python = 1,
            Django = 2,
        }

        public enum ReprKind {
            Normal = 0,
            Raw = 1,
            RawLen = 2,
        }

        public enum BreakpointConditionKind {
            Always = 0,
            WhenTrue = 1,
            WhenChanged = 2,
        }

        public enum BreakpointPassCountKind {
            Always = 0,
            Every = 1,
            WhenEqual = 2,
            WhenEqualOrGreater = 3,
        }

        public sealed class PythonObject {
            public string objRepr;
            public string hexRepr;
            public string typeName;
            public long length;
            public EvaluationResultFlags flags;
        }

        public sealed class ExceptionInfo {
            public string name;
            public int mode;
        }

        public sealed class EnumChildrenItem {
            public string name;
            public string expression;
            public PythonObject obj;
        }

        public sealed class ThreadFrameVariable {
            public string name;
            public PythonObject obj;
        }

        public sealed class ThreadFrameItem {
            public int startLine, endLine, lineNo, argCount;
            public string frameName, fileName;
            public FrameKind frameKind;
            public string djangoSourceFile; // optional, only if frame kind is django
            public int djangoSourceLine; // optional, only if frame kind is django
            public ThreadFrameVariable[] variables;
        }

        public sealed class ExceptionHandlerStatement {
            public int lineStart;
            public int lineEnd;
            public string[] expressions;
        }

        public sealed class ModuleItem {
            public string name;
            public string fileName;
        }
    }
}

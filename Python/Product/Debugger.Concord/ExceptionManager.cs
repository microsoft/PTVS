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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Exceptions;

namespace Microsoft.PythonTools.Debugger.Concord {
    internal class ExceptionManager : DkmDataItem {
        private readonly DkmProcess _process;
        private readonly HashSet<string> _monitoredExceptions = new HashSet<string>();

        public ExceptionManager(DkmProcess process) {
            _process = process;
        }

        public string GetAdditionalInformation(DkmExceptionInformation exception) {
            var customException = exception as DkmCustomExceptionInformation;
            if (customException == null || customException.AdditionalInformation == null) {
                return null;
            }

            return Encoding.Unicode.GetString(customException.AdditionalInformation.ToArray());
        }

        public string GetDescription(DkmExceptionInformation exception) {
            return exception.Name;
        }

        public void AddExceptionTrigger(DkmProcess process, Guid sourceId, DkmExceptionTrigger trigger) {
            var nameTrigger = trigger as DkmExceptionNameTrigger;
            if (nameTrigger != null && nameTrigger.ExceptionCategory == Guids.PythonExceptionCategoryGuid) {
                string name = nameTrigger.Name;
                bool wasEmpty = _monitoredExceptions.Count == 0;

                if (nameTrigger.ProcessingStage.HasFlag(DkmExceptionProcessingStage.Thrown) ||
                    nameTrigger.ProcessingStage.HasFlag(DkmExceptionProcessingStage.UserCodeSearch)
                ) {
                    _monitoredExceptions.Add(nameTrigger.Name);
                } else {
                    _monitoredExceptions.Remove(nameTrigger.Name);
                }

                bool isEmpty = _monitoredExceptions.Count == 0;
                if (wasEmpty != isEmpty) {
                    new LocalComponent.MonitorExceptionsRequest { MonitorExceptions = !isEmpty }.SendHigher(process);
                }
            }

            process.AddExceptionTrigger(sourceId, trigger);
        }

        public void ClearExceptionTriggers(DkmProcess process, Guid sourceId) {
            if (_monitoredExceptions.Count != 0) {
                _monitoredExceptions.Clear();
                new LocalComponent.MonitorExceptionsRequest { MonitorExceptions = false }.SendHigher(process);
            }

            process.ClearExceptionTriggers(sourceId);
        }
    }

    internal class ExceptionManagerLocalHelper : DkmDataItem {
        private readonly DkmProcess _process;
        private bool _monitorExceptions = true;

        // Breakpoints used to intercept Python exceptions when they're raised. These are enabled dynamically
        // when at least one exception is set to break on throw, and disabled when all exceptions are cleared.
        private readonly List<DkmRuntimeBreakpoint> _exceptionBreakpoints = new List<DkmRuntimeBreakpoint>();

        public ExceptionManagerLocalHelper(DkmProcess process) {
            _process = process;

            // In Dev12, AddExceptionTrigger is not consistently called when user updates exception settings, and
            // so we cannot use it to figure out whether we need to monitor or not, so this setting is true and
            // never changes. In Dev14+, it is reliable, and so we begin with false, and flip it to true when we
            // see the first break-on-throw exception trigger come in.
            _monitorExceptions = false;
        }

        public void OnPythonRuntimeInstanceLoaded() {
            var pyrtInfo = _process.GetPythonRuntimeInfo();
            var handlers = new PythonDllBreakpointHandlers(this);
            _exceptionBreakpoints.AddRange(LocalComponent.CreateRuntimeDllFunctionExitBreakpoints(
                pyrtInfo.DLLs.Python, "PyErr_SetObject", handlers.PyErr_SetObject, enable: _monitorExceptions));
            if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                _exceptionBreakpoints.AddRange(LocalComponent.CreateRuntimeDllFunctionExitBreakpoints(
                    pyrtInfo.DLLs.Python, "do_raise", handlers.do_raise, enable: _monitorExceptions));
            }
        }

        public bool MonitorExceptions { 
            get {
                return _monitorExceptions;
            }
            set {
                if (_monitorExceptions != value) {
                    _monitorExceptions = value;
                    foreach (var bp in _exceptionBreakpoints) {
                        if (_monitorExceptions) {
                            bp.Enable();
                        } else {
                            bp.Disable();
                        }
                    }
                }
            }
        }

        public void OnException(DkmThread thread) {
            if (thread.SystemPart == null) {
                Debug.Fail("OnException couldn't obtain system thread ID.");
                return;
            }
            var tid = thread.SystemPart.Id;

            var process = thread.Process;
            PyThreadState tstate = PyThreadState.GetThreadStates(process).FirstOrDefault(ts => ts.thread_id.Read() == tid);
            if (tstate == null) {
                Debug.Fail("OnException couldn't find PyThreadState corresponding to system thread " + tid);
                return;
            }

            var exc_type = tstate.curexc_type.TryRead();
            var exc_value = tstate.curexc_value.TryRead();
            if (exc_type == null || exc_type.IsNone) {
                return;
            }

            var reprOptions = new ReprOptions(process);

            string typeName = Strings.DebugUnknownExceptionType;
            string additionalInfo = "";
            try {
                var typeObject = exc_type as PyTypeObject;
                if (typeObject != null) {
                    var mod = typeObject.__module__;
                    var ver = _process.GetPythonRuntimeInfo().LanguageVersion;
                    if ((mod == "builtins" && ver >= PythonLanguageVersion.V30) ||
                        (mod == "exceptions" && ver < PythonLanguageVersion.V30)) {

                        typeName = typeObject.__name__;
                    } else {
                        typeName = mod + "." + typeObject.__name__;
                    }
                }

                var exc = exc_value as PyBaseExceptionObject;
                if (exc != null) {
                    var args = exc.args.TryRead();
                    if (args != null) {
                        additionalInfo = args.Repr(reprOptions);
                    }
                } else {
                    var str = exc_value as IPyBaseStringObject;
                    if (str != null) {
                        additionalInfo = str.ToString();
                    } else if (exc_value != null) {
                        additionalInfo = exc_value.Repr(reprOptions);
                    }
                }
            } catch {
            }

            new RemoteComponent.RaiseExceptionRequest
            {
                ThreadId = thread.UniqueId,
                Name = typeName,
                AdditionalInformation = Encoding.Unicode.GetBytes(additionalInfo)
            }.SendLower(process);
        }

        private class PythonDllBreakpointHandlers {
            private readonly ExceptionManagerLocalHelper _owner;

            public PythonDllBreakpointHandlers(ExceptionManagerLocalHelper owner) {
                _owner = owner;
            }

            public void PyErr_SetObject(DkmThread thread, ulong frameBase, ulong vframe, ulong returnAddress) {
                _owner.OnException(thread);
            }

            public void do_raise(DkmThread thread, ulong frameBase, ulong vframe, ulong returnAddress) {
                _owner.OnException(thread);
            }
        }
    }
}

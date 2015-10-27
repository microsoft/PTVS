// Visual Studio Shared Project
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
using System.Linq;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudioTools {
    /// <summary>
    /// Registers an exception in the Debug->Exceptions window.
    /// 
    /// Supports hierarchy registration but all elements of the hierarchy also need
    /// to be registered independently (to provide their code/state settings).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideDebugExceptionAttribute : RegistrationAttribute {
        // EXCEPTION_STATE flags that are valid for DKM exception entries (directly under the engine key)
        private const enum_EXCEPTION_STATE DkmValidFlags =
            enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE |
            enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE |
            enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_FIRST_CHANCE |
            enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;

        private readonly string _engineGuid;
        private readonly string _category;
        private readonly string[] _path;
        private int _code;
        private enum_EXCEPTION_STATE _state;

        public ProvideDebugExceptionAttribute(string engineGuid, string category, params string[] path) {
            _engineGuid = engineGuid;
            _category = category;
            _path = path;
            _state = enum_EXCEPTION_STATE.EXCEPTION_JUST_MY_CODE_SUPPORTED | enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
        }

        public int Code {
            get {
                return _code;
            }
            set {
                _code = value;
            }
        }

        public enum_EXCEPTION_STATE State {
            get {
                return _state;
            }
            set {
                _state = value;
            }
        }

        public bool BreakByDefault {
            get {
                return _state.HasFlag(enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT);
            }
            set {
                if (value) {
                    _state |= enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
                } else {
                    _state &= ~enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
                }
            }
        }

        public override void Register(RegistrationAttribute.RegistrationContext context) {
            var engineKey = context.CreateKey("AD7Metrics\\Exception\\" + _engineGuid);

            var key = engineKey.CreateSubkey(_category);
            foreach (var pathElem in _path) {
                key = key.CreateSubkey(pathElem);
            }
            key.SetValue("Code", _code);
            key.SetValue("State", (int)_state);

            // Debug engine load time can be improved by writing the exception category default 
            // stop setting and exceptions to the default settings at the exception category reg 
            // key node. This improves debug engine load time by getting necessary exception stop
            // settings for the entire category without having to enumerate the entire category 
            // hive structure when loading the debug engine.
            string name = _path.LastOrDefault();
            if (name == null || !BreakByDefault) {
                engineKey.SetValue(name ?? "*", (int)(_state & DkmValidFlags));
            }
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
        }
    }
}

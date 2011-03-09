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
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Registers an exception in the Debug->Exceptions window.
    /// 
    /// Supports hierarchy registration but all elements of the hierarchy also need
    /// to be registered independently (to provide their code/state settings).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]
    class ProvideDebugExceptionAttribute : RegistrationAttribute {
        private readonly string _engineGuid;
        private readonly string[] _path;
        private int _code, _state;

        public ProvideDebugExceptionAttribute(string engineGuid, params string[] path) {
            _engineGuid = engineGuid;
            _path = path;
        }

        public int Code {
            get {
                return _code;
            }
            set {
                _code = value;
            }
        }

        public int State {
            get {
                return _state;
            }
            set {
                _state = value;
            }
        }

        public override void Register(RegistrationAttribute.RegistrationContext context) {
            var engineKey = context.CreateKey("AD7Metrics\\Exception\\" + _engineGuid);
            var key = engineKey;
            foreach (var pathElem in _path) {
                key = key.CreateSubkey(pathElem);
            }

            key.SetValue("Code", _code);
            key.SetValue("State", _state);
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
        }
    }
}

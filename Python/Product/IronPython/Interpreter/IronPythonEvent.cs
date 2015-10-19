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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonEvent : PythonObject, IPythonEvent {
        private IPythonType _eventHandlerType;
        private IList<IPythonType> _parameterTypes;

        public IronPythonEvent(IronPythonInterpreter interpreter, ObjectIdentityHandle eventObj)
            : base(interpreter, eventObj) {
        }

        #region IPythonEvent Members

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Event; }
        }

        public IPythonType EventHandlerType {
            get {
                if (_eventHandlerType == null) {
                    var ri = RemoteInterpreter;
                    _eventHandlerType = ri != null ? Interpreter.GetTypeFromType(ri.GetEventPythonType(Value)) : null;
                }
                return _eventHandlerType;
            }
        }

        public IList<IPythonType> GetEventParameterTypes() {
            if (_parameterTypes == null) {
                var ri = RemoteInterpreter;
                var types = ri != null ? ri.GetEventParameterPythonTypes(Value) : new ObjectIdentityHandle[0];

                var paramTypes = new IPythonType[types.Length];
                for (int i = 0; i < paramTypes.Length; i++) {
                    paramTypes[i] = Interpreter.GetTypeFromType(types[i]);
                }

                _parameterTypes = paramTypes;
            }
            return _parameterTypes;
        }

        public string Documentation {
            get {
                var ri = RemoteInterpreter;
                return ri != null ? ri.GetEventDocumentation(Value) : string.Empty;
            }
        }

        #endregion
    }
}

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
using IronPython.Runtime.Types;
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
                    _eventHandlerType = Interpreter.GetTypeFromType(Interpreter.Remote.GetEventPythonType(Value));
                }
                return _eventHandlerType;
            }
        }

        public IList<IPythonType> GetEventParameterTypes() {
            if (_parameterTypes == null) {
                var types = Interpreter.Remote.GetEventParameterPythonTypes(Value);

                var paramTypes = new IPythonType[types.Length];
                for (int i = 0; i < paramTypes.Length; i++) {
                    paramTypes[i] = Interpreter.GetTypeFromType(types[i]);
                }

                _parameterTypes = paramTypes;
            }
            return _parameterTypes;
        }

        public string Documentation {
            get { return Interpreter.Remote.GetEventDocumentation(Value); }
        }

        #endregion
    }
}

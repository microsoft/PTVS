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
    class IronPythonEvent : PythonObject<ReflectedEvent>, IPythonEvent {
        public IronPythonEvent(IronPythonInterpreter interpreter, ReflectedEvent eventObj)
            : base(interpreter, eventObj) {
        }

        #region IPythonEvent Members

        public IPythonType EventHandlerType {
            get { return Interpreter.GetTypeFromType(Value.Info.EventHandlerType); }
        }

        public IList<IPythonType> GetEventParameterTypes() {
            var parameters = Value.Info.EventHandlerType.GetMethod("Invoke").GetParameters();
            var res = new IPythonType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                res[i] = Interpreter.GetTypeFromType(parameters[i].ParameterType);
            }
            return res;
        }

        public string Documentation {
            get { return Value.__doc__;  }
        }

        #endregion
    }
}

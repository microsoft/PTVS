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
using IronPython.Runtime;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinModule : IronPythonModule, IBuiltinPythonModule {

        public IronPythonBuiltinModule(IronPythonInterpreter interpreter, ObjectIdentityHandle mod, string name)
            : base(interpreter, mod, name) {
        }

        public IMember GetAnyMember(string name) {
            switch (name) {
                case "NoneType": return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                case "generator": return Interpreter.GetBuiltinType(BuiltinTypeId.Generator);
                case "builtin_function": return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinFunction);
                case "builtin_method_descriptor": return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinMethodDescriptor);
                case "dict_keys": return Interpreter.GetBuiltinType(BuiltinTypeId.DictKeys);
                case "dict_values": return Interpreter.GetBuiltinType(BuiltinTypeId.DictValues);
                case "function": return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
                case "ellipsis": return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
            }

            return base.GetMember(null, name);
        }
    }
}

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
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreterFactory : PythonInterpreterFactoryWithDatabase, ICustomInterpreterSerialization {
        public IronPythonInterpreterFactory(InterpreterConfiguration config, InterpreterFactoryCreationOptions options)
            : base(config, options) { }

        private IronPythonInterpreterFactory(Dictionary<string, object> properties)
            : base(InterpreterConfiguration.FromDictionary(properties), InterpreterFactoryCreationOptions.FromDictionary(properties)){
        }

        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new IronPythonInterpreter(factory);
        }

        bool ICustomInterpreterSerialization.GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = Configuration.ToDictionary();
            foreach (var kv in CreationOptions.ToDictionary()) {
                properties[kv.Key] = kv.Value;
            }
            return true;
        }
    }

    class IronPythonAstInterpreterFactory : AstPythonInterpreterFactory {
        public IronPythonAstInterpreterFactory(InterpreterConfiguration config, InterpreterFactoryCreationOptions options)
            : base(config, options) {
        }

        private IronPythonAstInterpreterFactory(Dictionary<string, object> properties)
            : base(InterpreterConfiguration.FromDictionary(properties), InterpreterFactoryCreationOptions.FromDictionary(properties)) {
        }

        public override IPythonInterpreter CreateInterpreter() {
            return new IronPythonInterpreter(this);
        }
    }
}

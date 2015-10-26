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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Uwp.Interpreter {
    class PythonUwpInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        public const string InterpreterGuidString = "{86767848-40B4-4007-8BCC-A3835EDF0E69}";
        public static readonly Guid InterpreterGuid = new Guid(InterpreterGuidString);

        public PythonUwpInterpreterFactory(InterpreterConfiguration configuration, string description) 
            : base(
                  InterpreterGuid,
                  description,
                  configuration,
                  true) {
        }

        public override string Description {
            get {
                return base.Description;
            }
        }
        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new PythonUwpInterpreter(factory);
        }
    }
}
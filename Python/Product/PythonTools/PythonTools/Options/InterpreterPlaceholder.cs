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
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    class InterpreterPlaceholder : IPythonInterpreterFactory {
        public const string PlaceholderId = "Placeholder";
        public InterpreterPlaceholder(string id, string description) {
            Configuration = new InterpreterConfiguration(
                PlaceholderId + ";" + id.ToString(),
                description,
                null,
                null,
                null,
                null,
                null,
                ProcessorArchitecture.None,
                new Version(),
                InterpreterUIMode.Normal
            );
        }
        
        public InterpreterConfiguration Configuration { get; private set; }

        public Guid Id {
            get {
                return Guid.Empty;
            }
        }

        public IPythonInterpreter CreateInterpreter() {
            throw new NotSupportedException();
        }

        public IPythonInterpreterFactoryProvider Provider {
            get {
                throw new NotSupportedException();
            }
        }
    }
}

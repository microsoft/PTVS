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

namespace Microsoft.PythonTools.Interpreter {
    public sealed class NoInterpretersFactory : IPythonInterpreterFactory {

        public NoInterpretersFactory() {
            Configuration = new InterpreterConfiguration(
                InterpreterRegistryConstants.NoInterpretersFactoryId,
                Strings.NoInterpretersDescription,
                uiMode: InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
            );
        }

        public InterpreterConfiguration Configuration { get; }

        public void NotifyImportNamesChanged() { }

        public IPythonInterpreter CreateInterpreter() {
            return new NoInterpretersInterpreter();
        }
    }

}

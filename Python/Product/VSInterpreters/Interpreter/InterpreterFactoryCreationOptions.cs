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

using System.Diagnostics;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Specifies creation options for an interpreter factory.
    /// </summary>
    public sealed class InterpreterFactoryCreationOptions {
        public InterpreterFactoryCreationOptions() {
#if DEBUG
            TraceLevel = TraceLevel.Verbose;
#endif
        }

        public InterpreterFactoryCreationOptions Clone() {
            return (InterpreterFactoryCreationOptions)MemberwiseClone();
        }

        public bool WatchFileSystem { get; set; }

        public string DatabasePath { get; set; }

        public bool UseExistingCache { get; set; } = true;

        public TraceLevel TraceLevel { get; set; } = TraceLevel.Info;
    }
}

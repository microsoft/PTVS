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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Represents the arguments for a Python profiling command.
    /// </summary>
    [Export(typeof(IPythonProfilingCommandArgs))]
    public class PythonProfilingCommandArgs : IPythonProfilingCommandArgs {
        public string PythonExePath { get; set; }
        public string WorkingDir { get; set; }
        public string ScriptPath { get; set; }
        public string[] Args { get; set; }
        public Dictionary<string, string> EnvVars { get; set; }
    }
}


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

namespace Microsoft.PythonTools.Profiling {

    /// <summary>
    /// Represents a command generated for a profiling target.
    /// </summary>
    public class TargetCommand {
        public string PythonExePath { get; set; }
        public string WorkingDir { get; set; }
        public string ScriptPath { get; set; }
        public string Args { get; set; }
        public Dictionary<string, string> EnvVars { get; set; }
    }

    /// <summary>
    /// Defines a service interface for collecting user input to construct a profiling target command.
    /// </summary>
    public interface IUserInputService {
        /// <summary>
        /// Collects user input via a dialog and converts it into a <see cref="TargetCommand"/>.
        /// </summary>
        TargetCommand GetCommandFromUserInput();
    }
}

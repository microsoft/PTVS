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

namespace Microsoft.PythonTools.Profiling {

    /// <summary>
    /// Defines a service interface for collecting user input and converting to Python profiling command arguments.
    /// </summary>
    public interface IPythonProfilerCommandService {
        /// <summary>
        /// Collects user input via a dialog and converts it into a <see cref="IPythonProfilingCommandArgs"/>.
        /// </summary>
        IPythonProfilingCommandArgs GetCommandArgsFromUserInput();
    }
}

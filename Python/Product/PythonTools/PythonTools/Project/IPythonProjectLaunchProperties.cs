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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Defines an interface for providing Python-specific launch parameters.
    /// </summary>
    public interface IPythonProjectLaunchProperties : IProjectLaunchProperties {
        /// <summary>
        /// Gets the path of the interpreter to launch. May throw an exception
        /// if no interpreter is available.
        /// </summary>
        string GetInterpreterPath();

        /// <summary>
        /// Gets the arguments to provide to the interpreter.
        /// </summary>
        string GetInterpreterArguments();
        
        /// <summary>
        /// True if the project does not start with a console window.
        /// </summary>
        bool? GetIsWindowsApplication();

        /// <summary>
        /// True if mixed-mode debugging should be used.
        /// </summary>
        bool? GetIsNativeDebuggingEnabled();
    }
}

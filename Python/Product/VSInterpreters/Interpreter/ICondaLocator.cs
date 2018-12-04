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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides the ability to find a conda executable to use for conda
    /// environment management and package installation.
    /// </summary>
    public interface ICondaLocator {
        /// <summary>
        /// Returns an absolute path to a conda executable.
        /// Can be <c>null</c> or <see cref="System.String.Empty"/> if no conda
        /// executable is found/available.
        /// </summary>
        string CondaExecutablePath { get; }
    }

    public interface ICondaLocatorMetadata {
        /// <summary>
        /// Priority determines which of the conda locators available should be used.
        /// Lower value means higher priority.
        /// </summary>
        int Priority { get; }
    }
}

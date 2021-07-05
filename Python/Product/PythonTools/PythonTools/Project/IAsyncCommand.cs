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

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Defines a command that also has an ExecuteAsync method.
    /// </summary>
    public interface IAsyncCommand : ICommand {
        /// <summary>
        /// Defines the method to be called when the command is invoked
        /// asynchronously.
        /// </summary>
        /// <param name="parameter">
        /// Data used by the command. If the command does not require data to be
        /// passed, this object can be set to null.
        /// </param>
        /// <returns>
        /// A task that will complete when the command has completed or faulted.
        /// </returns>
        Task ExecuteAsync(object parameter);
    }
}

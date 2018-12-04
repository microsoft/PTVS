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

using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    public interface IPackageManagerUI {
        /// <summary>
        /// Called when output text should be displayed to the user.
        /// </summary>
        /// <param name="text">
        /// The text to display. Trailing newlines will be included.
        /// </param>
        /// <remarks>This function may be called from any thread.</remarks>
        void OnOutputTextReceived(IPackageManager sender, string text);

        /// <summary>
        /// Called when error text should be displayed to the user.
        /// </summary>
        /// <param name="text">
        /// The text to display. Trailing newlines will be included.
        /// </param>
        /// <remarks>This function may be called from any thread.</remarks>
        void OnErrorTextReceived(IPackageManager sender, string text);

        /// <summary>
        /// Called when an operation starts.
        /// </summary>
        /// <param name="operation">
        /// An operation identifier. This is intended for logging rather than
        /// user information.
        /// </param>
        /// <remarks>This function may be called from any thread.</remarks>
        void OnOperationStarted(IPackageManager sender, string operation);

        /// <summary>
        /// Called when an operation completes.
        /// </summary>
        /// <param name="operation">
        /// An operation identifier. This is intended for logging rather than
        /// user information.
        /// </param>
        /// <param name="success">True if the operation was successful.</param>
        /// <remarks>This function may be called from any thread.</remarks>
        void OnOperationFinished(IPackageManager sender, string operation, bool success);

        /// <summary>
        /// Called when the package manager needs to decide whether to elevate.
        /// This function may display UI to users, but should not attempt to
        /// elevate.
        /// </summary>
        /// <param name="operation">The current operation identifier.</param>
        /// <returns>True if the operation should elevate.</returns>
        /// <remarks>This function may be called from any thread.</remarks>
        Task<bool> ShouldElevateAsync(IPackageManager sender, string operation);
    }
}

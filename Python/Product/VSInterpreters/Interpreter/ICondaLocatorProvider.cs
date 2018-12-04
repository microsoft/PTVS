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
    /// Provides the ability to find the most appropriate conda locator from
    /// the many that may be available.
    /// </summary>
    public interface ICondaLocatorProvider {
        /// <summary>
        /// Retrieves the highest priority conda locator available.
        /// </summary>
        /// <returns>
        /// A locator that has a valid conda executable, or <c>null</c>
        /// if none was found.
        /// </returns>
        ICondaLocator FindLocator();
    }
}

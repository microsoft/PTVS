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

namespace Microsoft.CookiecutterTools.Interpreters {
    /// <summary>
    /// Provides a source of Python interpreters.  This enables a single implementation
    /// to dynamically lookup the installed Python versions and provide multiple interpreters.
    /// </summary>
    public interface IPythonInterpreterFactoryProvider {
        /// <summary>
        /// Raised when the result of calling <see cref="GetInterpreterConfigurations"/> may have changed.
        /// </summary>
        /// <remarks>New in 2.0.</remarks>
        event EventHandler InterpreterFactoriesChanged;


        /// <summary>
        /// Returns the interpreter configurations that this provider supports.  
        /// 
        /// The configurations returned should be the same instances for subsequent calls.  If the number 
        /// of available configurations can change at runtime new factories can still be returned but the 
        /// existing instances should not be re-created.
        /// </summary>
        IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations();

        /// <summary>
        /// Gets a specific configured interpreter
        /// </summary>
        IPythonInterpreterFactory GetInterpreterFactory(string id);

        /// <summary>
        /// Gets a property value associated with the specified interpreter. If
        /// the property is not set or available, return <c>null</c>.
        /// 
        /// Property values should not change over the process lifetime.
        /// </summary>
        /// <param name="id">The interpreter id.</param>
        /// <param name="propName">A case-sensitive string identifying the name
        /// of the property. Values will be compared by ordinal.</param>
        object GetProperty(string id, string propName);
    }
}
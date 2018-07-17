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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Interface for providing an interpreter implementation for plugging into
    /// Python support for Visual Studio.
    /// 
    /// This interface provides information about Python types and modules,
    /// which will be used for program analysis and IntelliSense.
    /// 
    /// An interpreter is provided by an object implementing 
    /// <see cref="IPythonInterpreterFactory"/>.
    /// </summary>
    public interface IPythonInterpreter : IDisposable {
        /// <summary>
        /// Performs any interpreter-specific initialization that is required.
        /// </summary>
        /// <param name="state"></param>
        void Initialize(PythonAnalyzer state);

        /// <summary>
        /// Gets a well known built-in type such as int, list, dict, etc...
        /// </summary>
        /// <param name="id">The built-in type to get</param>
        /// <returns>An IPythonType representing the type.</returns>
        /// <exception cref="KeyNotFoundException">
        /// The requested type cannot be resolved by this interpreter.
        /// </exception>
        IPythonType GetBuiltinType(BuiltinTypeId id);

        /// <summary>
        /// Returns a list of module names that can be imported by this
        /// interpreter.
        /// </summary>
        IList<string> GetModuleNames();

        /// <summary>
        /// The list of built-in module names has changed (usually because a
        /// background analysis of the standard library has completed).
        /// </summary>
        event EventHandler ModuleNamesChanged;

        /// <summary>
        /// Synchronous variant of <see ref="IPythonInterpreter2.ImportModuleAsync">.
        /// Waits for the async import completion using timeouts.
        /// </summary>
        IPythonModule ImportModule(string name);

        /// <summary>
        /// Provides interpreter-specific information which can be associated
        /// with a module.
        /// 
        /// Interpreters can return null if they have no per-module state.
        /// </summary>
        IModuleContext CreateModuleContext();
    }

    public interface IPythonInterpreter2: IPythonInterpreter {
        /// <summary>
        /// Returns an IPythonModule for a given module name. Returns null if
        /// the module does not exist. The import is performed asynchronously.
        /// </summary>
        Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token);
    }
}

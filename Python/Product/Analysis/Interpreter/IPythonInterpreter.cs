/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Interface for providing an interpreter implementation for plugging into
    /// Python Tools for Visual Studio.
    /// 
    /// This interface provides information about Python types and modules,
    /// which will be used for program analysis and IntelliSense.
    /// 
    /// An interpreter is provided by an object implementing 
    /// <see cref="IPythonInterpreterFactory"/>.
    /// </summary>
    public interface IPythonInterpreter {
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
        /// Returns an IPythonModule for a given module name. Returns null if
        /// the module does not exist.
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
}

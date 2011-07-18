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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Interface for providing an interpreter implementation for plugging into Python Tools for Visual Studio.
    /// 
    /// This interface provides meta-data for completion, support for executing code, and interacting with a REPL.
    /// 
    /// The interpreter is provided via the IPythonInterpreterProvider interface.  This interface can inspect the
    /// system for available interpreters that it can support (e.g. different verions) and provide different
    /// IPythonInterpreter objects for each one.  The user is then able to select which interprerter they use.
    /// </summary>
    public interface IPythonInterpreter {

        #region Analysis Support

        void Initialize(IInterpreterState state);

        /// <summary>
        /// Gets a well known built-in type such as int, list, dict, etc...
        /// </summary>
        /// <param name="id">The built-in type to get</param>
        /// <returns>An IPythonType object which represents the Python type.</returns>
        IPythonType GetBuiltinType(BuiltinTypeId id);

        /// <summary>
        /// Returns a list of module names (both built-in and cached modules which have been analyzed).
        /// </summary>
        IList<string> GetModuleNames();

        /// <summary>
        /// The list of built-in module names has changed (usually because a background analysis of the standard library has changed).
        /// </summary>
        event EventHandler ModuleNamesChanged;

        /// <summary>
        /// Returns the IPythonModule for a given module name.  If the module does not exist null should be returned.
        /// </summary>
        IPythonModule ImportModule(string name);

        /// <summary>
        /// Provides interpreter specific information which can be associated with a module.
        /// 
        /// It's safe for the interpreter to return null here if it has no per-module state.
        /// </summary>
        IModuleContext CreateModuleContext();

        #endregion
    }
}

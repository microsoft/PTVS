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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides a factory for creating IPythonInterpreters for a specific
    /// Python implementation.
    /// 
    /// The factory includes information about what type of interpreter will be
    /// created - this is used for displaying information to the user and for
    /// tracking per-interpreter settings.
    /// 
    /// It also contains a method for creating an interpreter. This allows for
    /// stateful interpreters that participate in analysis or track other state.
    /// </summary>
    public interface IPythonInterpreterFactory {
        /// <summary>
        /// Configuration settings for the interpreter.
        /// </summary>
        InterpreterConfiguration Configuration {
            get;
        }

        /// <summary>
        /// Creates an IPythonInterpreter instance.
        /// </summary>
        IPythonInterpreter CreateInterpreter();

        /// <summary>
        /// Notifies the interpreter factory that the set of names that
        /// can be imported may have changed.
        /// </summary>
        void NotifyImportNamesChanged();
    }
}

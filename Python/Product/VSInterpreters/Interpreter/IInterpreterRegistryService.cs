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

namespace Microsoft.PythonTools.Interpreter {
    public interface IInterpreterRegistryService {
        /// <summary>
        /// Returns a sequence of available interpreters. The sequence is sorted
        /// and should not be re-sorted if it will be displayed to users.
        /// </summary>
        IEnumerable<IPythonInterpreterFactory> Interpreters { get; }

        IEnumerable<InterpreterConfiguration> Configurations { get; }

        /// <summary>
        /// Returns a sequence of available interpreters. If no interpreters are
        /// available, the sequence contains only
        /// <see cref="NoInterpretersValue"/>.
        /// </summary>
        IEnumerable<IPythonInterpreterFactory> InterpretersOrDefault { get; }

        /// <summary>
        /// Gets the factory that represents the state when no factories are
        /// available.
        /// </summary>
        IPythonInterpreterFactory NoInterpretersValue { get; }

        IPythonInterpreterFactory FindInterpreter(string id);

        InterpreterConfiguration FindConfiguration(string id);

        /// <summary>
        /// Gets a property value relating to a specific interpreter.
        /// 
        /// If the property is not set, returns <c>null</c>.
        /// </summary>
        /// <param name="id">The interpreter identifier.</param>
        /// <param name="propName">A case-sensitive string identifying the
        /// property. Values will be compared by ordinal.</param>
        /// <returns>The property value, or <c>null</c> if not set.</returns>
        object GetProperty(string id, string propName);

        /// <summary>
        /// Raised when the set of interpreters changes. This is not raised when
        /// the set is first initialized.
        /// </summary>
        event EventHandler InterpretersChanged;

        /// <summary>
        /// Raised when all async interpreter factory providers have completed discovering interpreters
        /// </summary>
        event EventHandler InterpreterDiscoveryCompleted;

        /// <summary>
        /// Called to suppress the <see cref="InterpretersChanged"/> event while
        /// making changes to the registry. If the event is triggered while
        /// suppressed, it will not be raised until suppression is lifted.
        /// 
        /// <see cref="EndSuppressInterpretersChangedEvent"/> must be called
        /// once for every call to this function.
        /// </summary>
        void BeginSuppressInterpretersChangedEvent();

        /// <summary>
        /// Lifts the suppression of the <see cref="InterpretersChanged"/> event
        /// initiated by <see cref="BeginSuppressInterpretersChangedEvent"/>.
        /// 
        /// This must be called once for every call to
        /// <see cref="BeginSuppressInterpretersChangedEvent"/>.
        /// </summary>
        void EndSuppressInterpretersChangedEvent();
    }
}

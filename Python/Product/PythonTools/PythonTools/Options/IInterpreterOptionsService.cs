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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides information about the available interpreters and the current
    /// default. Instances of this service should be obtained using MEF.
    /// </summary>
    public interface IInterpreterOptionsService {
        /// <summary>
        /// Returns a sequence of available interpreters. The sequence is sorted
        /// and should not be re-sorted if it will be displayed to users.
        /// </summary>
        IEnumerable<IPythonInterpreterFactory> Interpreters { get; }

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

        /// <summary>
        /// Returns an interpreter matching the provided ID and version, if one
        /// is available. Otherwise, return null.
        /// </summary>
        IPythonInterpreterFactory FindInterpreter(Guid id, Version version);

        /// <summary>
        /// Returns an interpreter matching the provided ID and version, if one
        /// is available. Otherwise, return null.
        /// </summary>
        IPythonInterpreterFactory FindInterpreter(Guid id, string version);

        /// <summary>
        /// Returns an interpreter matching the provided ID and version, if one
        /// is available. Otherwise, return null.
        /// </summary>
        IPythonInterpreterFactory FindInterpreter(string id, string version);

        /// <summary>
        /// Gets the set of known providers.
        /// </summary>
        IEnumerable<IPythonInterpreterFactoryProvider> KnownProviders { get; }

        /// <summary>
        /// Raised when the set of interpreters changes. This is not raised when
        /// the set is first initialized.
        /// </summary>
        event EventHandler InterpretersChanged;

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

        /// <summary>
        /// Gets or sets the default interpreter.
        /// </summary>
        IPythonInterpreterFactory DefaultInterpreter { get; set; }

        /// <summary>
        /// Raised when the default interpreter is set to a new value.
        /// </summary>
        event EventHandler DefaultInterpreterChanged;
    }
}

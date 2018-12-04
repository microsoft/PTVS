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

using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter {

    /// <summary>
    /// Provides information about the available interpreters and the current
    /// default. Instances of this service should be obtained using MEF.
    /// </summary>
    public interface IInterpreterOptionsService {
        /// <summary>
        /// Gets or sets the default interpreter.
        /// </summary>
        IPythonInterpreterFactory DefaultInterpreter { get; set; }

        /// <summary>
        /// Gets or sets the default interpreter by its id.
        /// </summary>
        string DefaultInterpreterId { get; set; }

        /// <summary>
        /// Raised when the default interpreter is set to a new value.
        /// </summary>
        event EventHandler DefaultInterpreterChanged;

        /// <summary>
        /// Adds or updates a new user configured interpreter factory to the
        /// registry stored under the provided name. The id in the configuration
        /// is ignored and the newly registered id is returned.
        /// </summary>
        string AddConfigurableInterpreter(string name, InterpreterConfiguration config);

        /// <summary>
        /// Removes a user configured interpreter factory.
        /// </summary>
        void RemoveConfigurableInterpreter(string id);

        /// <summary>
        /// Returns True if the interpreter factory with the specified id can be
        /// configured or removed.
        /// </summary>
        bool IsConfigurable(string id);

        /// <summary>
        /// Returns a sequence of zero or more package managers that can be used
        /// with the specified factory.
        /// </summary>
        IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory);
    }
}

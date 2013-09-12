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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides information to PythonTypeDatabase on how to generate a
    /// database.
    /// </summary>
    public sealed class PythonTypeDatabaseCreationRequest {
        public PythonTypeDatabaseCreationRequest() {
            ExtraInputDatabases = new List<string>();
        }

        /// <summary>
        /// The interpreter factory to use. This will provide language version
        /// and source paths.
        /// </summary>
        public PythonInterpreterFactoryWithDatabase Factory { get; set; }

        /// <summary>
        /// A list of extra databases to load when analyzing the factory's
        /// library.
        /// </summary>
        public List<string> ExtraInputDatabases { get; private set; }

        /// <summary>
        /// The directory to write the database to.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// True to avoid analyzing packages that are up to date; false to
        /// regenerate the entire database.
        /// </summary>
        public bool SkipUnchanged { get; set; }

        /// <summary>
        /// A factory to wait for before starting regeneration.
        /// </summary>
        public IPythonInterpreterFactoryWithDatabase WaitFor { get; set; }

        /// <summary>
        /// A function to call when the analysis process is completed. The value
        /// is an error code.
        /// </summary>
        public Action<int> OnExit { get; set; }
    }
}
